using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Data;
using Backend.Enums;
using Backend.Extensions;
using Files.Models;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Backend.Services
{
    /// <summary>
    /// Service for synchronizing statuses between related entities
    /// </summary>
    public class StatusSynchronizationService : IStatusSynchronizationService
    {
        private readonly RavenDbContext _context;
        private readonly ILogger<StatusSynchronizationService> _logger;

        public StatusSynchronizationService(RavenDbContext context, ILogger<StatusSynchronizationService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Synchronizes file statuses with the process status
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SynchronizeFileStatusesWithProcessAsync(string processId)
        {
            if (string.IsNullOrEmpty(processId))
            {
                throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
            }

            _logger.LogInformation("Synchronizing file statuses for process {ProcessId}", processId);

            // Ensure processId has the correct format
            if (!processId.StartsWith("processes/"))
            {
                processId = $"processes/{processId}";
            }

            // Get the process to determine its status
            ProcessStatus processStatus;
            using (var processSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
            {
                var process = await processSession.LoadAsync<Models.DeduplicationProcess>(processId);
                if (process == null)
                {
                    _logger.LogWarning("Process {ProcessId} not found during status synchronization", processId);
                    return;
                }

                processStatus = process.Status.ToProcessStatus();
            }

            // Get all files for this process
            var files = await GetFilesForProcessAsync(processId);
            if (files.Count == 0)
            {
                _logger.LogInformation("No files found to synchronize for process {ProcessId}", processId);
                return;
            }

            int updatedCount = 0;

            // Update file statuses based on process status
            using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
            {
                foreach (var file in files)
                {
                    var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
                    if (dbFile == null)
                    {
                        _logger.LogWarning("File with ID {FileId} not found during status synchronization", file.Id);
                        continue;
                    }

                    bool needsUpdate = false;
                    var currentFileStatus = dbFile.Status.ToFileStatus();
                    var currentFileProcessStatus = dbFile.ProcessStatus.ToFileProcessStatus();

                    // Set appropriate file status based on process status
                    switch (processStatus)
                    {
                        case ProcessStatus.Completed:
                            // If process is completed but file still shows as "Uploaded" or in processing state
                            if (currentFileStatus == FileStatus.Uploaded ||
                                currentFileProcessStatus == FileProcessStatus.Processing)
                            {
                                dbFile.Status = FileStatus.Inserted.ToDbString();
                                dbFile.ProcessStatus = FileProcessStatus.Completed.ToDbString();
                                needsUpdate = true;
                            }
                            break;

                        case ProcessStatus.Cleaned:
                            // If process is cleaned but file is not marked as deleted
                            if (currentFileStatus != FileStatus.Deleted)
                            {
                                dbFile.Status = FileStatus.Deleted.ToDbString();
                                dbFile.ProcessStatus = FileProcessStatus.Completed.ToDbString();
                                needsUpdate = true;
                            }
                            break;

                        case ProcessStatus.Error:
                            // If process has error, mark file process status accordingly
                            if (currentFileProcessStatus != FileProcessStatus.Failed)
                            {
                                dbFile.ProcessStatus = FileProcessStatus.Failed.ToDbString();
                                needsUpdate = true;
                            }
                            break;

                        case ProcessStatus.InProcessing:
                            // If process is in processing, update file process status
                            if (currentFileProcessStatus != FileProcessStatus.Processing)
                            {
                                dbFile.ProcessStatus = FileProcessStatus.Processing.ToDbString();
                                needsUpdate = true;
                            }
                            break;

                        case ProcessStatus.Paused:
                            // No specific action needed for paused processes
                            break;
                    }

                    if (needsUpdate)
                    {
                        updatedCount++;
                    }
                }

                // Save all changes at once if any files were updated
                if (updatedCount > 0)
                {
                    await fileSession.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} files to match process status {Status} for process {ProcessId}",
                        updatedCount, processStatus, processId);
                }
                else
                {
                    _logger.LogInformation("No file status updates needed for process {ProcessId}", processId);
                }
            }
        }

        /// <summary>
        /// Gets all files associated with a process
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <returns>A list of files</returns>
        private async Task<List<FileModel>> GetFilesForProcessAsync(string processId)
        {
            using var processSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);
            var process = await processSession.LoadAsync<Models.DeduplicationProcess>(processId);

            if (process == null || process.FileIds == null || !process.FileIds.Any())
            {
                return new List<FileModel>();
            }

            using var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);
            var files = new List<FileModel>();

            // Load files in batches to avoid loading too many at once
            const int batchSize = 100;
            for (int i = 0; i < process.FileIds.Count; i += batchSize)
            {
                var batch = process.FileIds.Skip(i).Take(batchSize).ToList();
                var batchFiles = await fileSession.LoadAsync<FileModel>(batch);
                files.AddRange(batchFiles.Values.Where(f => f != null));
            }

            return files;
        }

        /// <summary>
        /// Fixes inconsistencies in a completed process
        /// </summary>
        /// <param name="processId">The ID of the process to fix</param>
        /// <returns>True if fixes were applied, false otherwise</returns>
        public async Task<bool> FixProcessDataAsync(string processId)
        {
            try
            {
                _logger.LogInformation("Checking for inconsistencies in completed process {ProcessId}", processId);

                // Ensure processId has the correct format
                if (!processId.StartsWith("processes/"))
                {
                    processId = $"processes/{processId}";
                }

                // Get the process
                Models.DeduplicationProcess process;
                using (var processSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
                {
                    process = await processSession.LoadAsync<Models.DeduplicationProcess>(processId);
                    if (process == null)
                    {
                        _logger.LogWarning("Process {ProcessId} not found during consistency check", processId);
                        return false;
                    }

                    // Process all processes, not just completed ones
                    // This ensures all processes have consistent data
                    bool needsUpdate = false;

                    // Fix CompletedAt if null for completed processes
                    if (process.CompletedAt == null &&
                        (process.Status == ProcessStatus.Completed.ToDbString() || process.ProcessEndDate.HasValue))
                    {
                        process.CompletedAt = process.ProcessEndDate ?? DateTime.UtcNow;
                        needsUpdate = true;
                    }

                    // Fix FileCount if 0 or null
                    if (process.FileCount == 0 && process.FileIds != null && process.FileIds.Count > 0)
                    {
                        process.FileCount = process.FileIds.Count;
                        needsUpdate = true;
                    }

                    // Fix CreatedBy if null
                    if (string.IsNullOrEmpty(process.CreatedBy) && !string.IsNullOrEmpty(process.Username))
                    {
                        process.CreatedBy = process.Username;
                        needsUpdate = true;
                    }

                    // Fix CleanupUsername if null for cleaned processes
                    if (string.IsNullOrEmpty(process.CleanupUsername) &&
                        (process.Status == ProcessStatus.Cleaned.ToDbString() || process.CleanupDate.HasValue))
                    {
                        process.CleanupUsername = process.Username ?? "system";
                        needsUpdate = true;
                    }

                    // Fix CurrentStage if null
                    if (string.IsNullOrEmpty(process.CurrentStage))
                    {
                        process.CurrentStage = process.Status;
                        needsUpdate = true;
                    }

                    // Fix ProcessedFiles if 0 but CompletionNotes indicates files were processed
                    if (process.ProcessedFiles == 0 && process.FileIds != null && process.FileIds.Count > 0)
                    {
                        // First try to extract from completion notes if available
                        if (process.CompletionNotes != null && process.CompletionNotes.Contains("Processed"))
                        {
                            var processedText = process.CompletionNotes;
                            int startIndex = processedText.IndexOf("Processed ") + 10;
                            int endIndex = processedText.IndexOf(" files", startIndex);

                            if (startIndex > 10 && endIndex > startIndex)
                            {
                                string countText = processedText.Substring(startIndex, endIndex - startIndex);
                                if (int.TryParse(countText, out int count))
                                {
                                    process.ProcessedFiles = count;
                                    needsUpdate = true;
                                }
                            }
                        }

                        // If still 0, use FileIds count for completed processes
                        if (process.ProcessedFiles == 0 && process.Status == ProcessStatus.Completed.ToDbString())
                        {
                            process.ProcessedFiles = process.FileIds.Count;
                            needsUpdate = true;
                        }
                    }

                    // For completed processes, ensure CurrentStage is set correctly
                    if (process.Status == ProcessStatus.Completed.ToDbString() &&
                        (string.IsNullOrEmpty(process.CurrentStage) || process.CurrentStage != "Completed"))
                    {
                        process.CurrentStage = "Completed";
                        needsUpdate = true;
                    }

                    // Save process changes if needed
                    if (needsUpdate)
                    {
                        await processSession.SaveChangesAsync();
                        _logger.LogInformation("Fixed inconsistencies in process {ProcessId}", processId);
                    }
                }

                // Fix file statuses
                await SynchronizeFileStatusesWithProcessAsync(processId);

                // Fix file data (FaceIds, ProcessStartDate, etc.)
                await FixFileDataAsync(processId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing inconsistencies in process {ProcessId}", processId);
                return false;
            }
        }

        /// <summary>
        /// Fixes missing data in files for a process
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        private async Task FixFileDataAsync(string processId)
        {
            try
            {
                var files = await GetFilesForProcessAsync(processId);
                if (files.Count == 0)
                {
                    return;
                }

                int updatedCount = 0;
                using var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);

                foreach (var file in files)
                {
                    bool needsUpdate = false;

                    // Fix missing FaceId for completed files
                    if (string.IsNullOrEmpty(file.FaceId) &&
                        (file.Status == FileStatus.Inserted.ToDbString() || file.ProcessStatus == FileProcessStatus.Completed.ToDbString()))
                    {
                        // Generate a placeholder FaceId
                        file.FaceId = $"face_{Guid.NewGuid()}";
                        needsUpdate = true;
                    }

                    // Fix missing ProcessStartDate for completed files
                    if (file.ProcessStartDate == DateTime.MinValue &&
                        (file.Status == FileStatus.Inserted.ToDbString() || file.ProcessStatus == FileProcessStatus.Completed.ToDbString()))
                    {
                        file.ProcessStartDate = DateTime.UtcNow.AddMinutes(-5); // Set to 5 minutes ago
                        needsUpdate = true;
                    }

                    // Ensure ProcessStatus is Completed for inserted files
                    if (file.Status == FileStatus.Inserted.ToDbString() && file.ProcessStatus != FileProcessStatus.Completed.ToDbString())
                    {
                        file.ProcessStatus = FileProcessStatus.Completed.ToDbString();
                        needsUpdate = true;
                    }

                    // Set Photodeduplique flag for completed files
                    if ((file.Status == FileStatus.Inserted.ToDbString() || file.ProcessStatus == FileProcessStatus.Completed.ToDbString())
                        && !file.Photodeduplique)
                    {
                        file.Photodeduplique = true;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        updatedCount++;
                    }
                }

                if (updatedCount > 0)
                {
                    await fileSession.SaveChangesAsync();
                    _logger.LogInformation("Fixed {Count} files with missing data for process {ProcessId}",
                        updatedCount, processId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing file data for process {ProcessId}", processId);
            }
        }

        /// <summary>
        /// Validates if a status transition is allowed
        /// </summary>
        /// <param name="currentStatus">The current status</param>
        /// <param name="newStatus">The new status</param>
        /// <returns>True if the transition is valid, false otherwise</returns>
        public bool IsValidProcessStatusTransition(ProcessStatus currentStatus, ProcessStatus newStatus)
        {
            // Define valid transitions
            switch (currentStatus)
            {
                case ProcessStatus.ReadyToStart:
                    return newStatus == ProcessStatus.InProcessing ||
                           newStatus == ProcessStatus.Error;

                case ProcessStatus.InProcessing:
                    return newStatus == ProcessStatus.Completed ||
                           newStatus == ProcessStatus.Paused ||
                           newStatus == ProcessStatus.Error ||
                           newStatus == ProcessStatus.ConflictDetected;

                case ProcessStatus.Completed:
                    return newStatus == ProcessStatus.Cleaning ||
                           newStatus == ProcessStatus.Error;

                case ProcessStatus.Paused:
                    return newStatus == ProcessStatus.InProcessing ||
                           newStatus == ProcessStatus.Error;

                case ProcessStatus.Error:
                    // Can't transition from Error state except manually
                    return false;

                case ProcessStatus.ConflictDetected:
                    return newStatus == ProcessStatus.InProcessing ||
                           newStatus == ProcessStatus.Error;

                case ProcessStatus.Cleaning:
                    return newStatus == ProcessStatus.Cleaned ||
                           newStatus == ProcessStatus.Error;

                case ProcessStatus.Cleaned:
                    // Final state
                    return false;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Interface for the status synchronization service
    /// </summary>
    public interface IStatusSynchronizationService
    {
        Task SynchronizeFileStatusesWithProcessAsync(string processId);
        bool IsValidProcessStatusTransition(ProcessStatus currentStatus, ProcessStatus newStatus);
        Task<bool> FixProcessDataAsync(string processId);
    }
}
