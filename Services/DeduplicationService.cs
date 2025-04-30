using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Backend.Models;
using Backend.DTOs;
using Backend.Data;
using Backend.Interfaces;
using Files.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Linq;

namespace Backend.Services
{
  public class DeduplicationService : IDeduplicationService
  {
    private readonly RavenDbContext _context;
    private readonly IT4FaceService _t4FaceService;
    private readonly ConflictService _conflictService;
    private readonly ExceptionService _exceptionService;
    private readonly DuplicateRecordService _duplicateRecordService;
    private readonly ILogger<DeduplicationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _tempFilePath;

    public DeduplicationService(
        RavenDbContext context,
        IT4FaceService t4FaceService,
        ConflictService conflictService,
        ExceptionService exceptionService,
        DuplicateRecordService duplicateRecordService,
        ILogger<DeduplicationService> logger,
        IConfiguration configuration)
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _t4FaceService = t4FaceService ?? throw new ArgumentNullException(nameof(t4FaceService));
      _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));
      _exceptionService = exceptionService ?? throw new ArgumentNullException(nameof(exceptionService));
      _duplicateRecordService = duplicateRecordService ?? throw new ArgumentNullException(nameof(duplicateRecordService));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
      _tempFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles");

      // No identification threshold - we'll use the raw similarity values from the API

      Directory.CreateDirectory(_tempFilePath);
    }

    public async Task<DeduplicationProcess> StartDeduplicationProcessAsync(string username = null)
    {
      // Use the processes database explicitly
      using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);
      var currentUsername = username ?? "system";
      var process = new DeduplicationProcess
      {
        Id = $"DeduplicationProcesses/{Guid.NewGuid()}",
        Name = $"Process-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
        Username = currentUsername,
        CreatedBy = currentUsername,
        Status = "Ready to Start",
        CreatedAt = DateTime.UtcNow,
        Steps = new List<ProcessStep>(),
        FileIds = new List<string>(),
        FileCount = 0, // Will be updated when files are added
        ProcessedFiles = 0,
        CurrentStage = "Created",
        CompletionNotes = ""
      };

      await session.StoreAsync(process);
      await session.SaveChangesAsync();
      _logger.LogInformation("Created new deduplication process with ID {ProcessId}", process.Id);
      return process;
    }

    public async Task<DeduplicationProcess> StartProcessAsync(DeduplicationProcessDto request, string username = null)
    {
      // Use the processes database explicitly
      using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);
      var currentUsername = username ?? "system";

      // Ensure FileIds is not null and calculate the count
      var fileIds = request.FileIds ?? new List<string>();
      var fileCount = fileIds.Count;

      var process = new DeduplicationProcess
      {
        Id = $"processes/{Guid.NewGuid()}",
        Name = $"Process-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
        Username = currentUsername,
        CreatedBy = currentUsername,
        Status = "Ready to Start",
        CreatedAt = DateTime.UtcNow,
        Steps = new List<ProcessStep>(),
        FileIds = fileIds,
        FileCount = fileCount, // Explicitly set to match the number of files
        ProcessedFiles = 0,
        CurrentStage = "Created",
        CompletionNotes = ""
      };

      await session.StoreAsync(process);
      await session.SaveChangesAsync();
      _logger.LogInformation("Created new process with ID {ProcessId} and {FileCount} files", process.Id, process.FileCount);
      return process;
    }

    public async Task<List<ProcessDTO>> GetAllProcesses()
    {
      // Use the processes database explicitly
      using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);
      var query = session.Query<DeduplicationProcess>();
      var processes = await query.ToListAsync();

      _logger.LogInformation("Found {ProcessCount} processes in the database", processes.Count);

      return processes.Select(p => new ProcessDTO
      {
        Id = p.Id,
        Name = p.Name,
        Status = p.Status,
        CreatedAt = p.CreatedAt,
        Username = p.Username,
        TotalFiles = p.FileIds != null ? p.FileIds.Count : 0,
        ProcessedFiles = p.Steps != null ?
              p.Steps.Where(s => s.Name == "Insertion" && s.Status == "Completed")
              .SelectMany(s => s.ProcessedFiles ?? new List<string>()).Count() : 0
      }).ToList();
    }

    public async Task<DeduplicationProcess> GetProcessAsync(string processId)
    {
      // Clean up the process ID to handle potential format issues
      string cleanProcessId = processId.Trim();

      // Only use the processes database - don't search in the default (users) database
      using var processesSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);
      DeduplicationProcess? process = null;

      // List of possible ID formats to try
      var idFormatsToTry = new List<string>
      {
        cleanProcessId, // As is
        cleanProcessId.StartsWith("processes/") ? cleanProcessId : $"processes/{cleanProcessId}",
        cleanProcessId.StartsWith("DeduplicationProcesses/") ? cleanProcessId : $"DeduplicationProcesses/{cleanProcessId}"
      };

      // Try each format in the processes database
      foreach (var idFormat in idFormatsToTry)
      {
        _logger.LogInformation("Trying to load process with ID format: {IdFormat} in processes database", idFormat);
        process = await processesSession.LoadAsync<DeduplicationProcess>(idFormat);
        if (process != null)
        {
          _logger.LogInformation("Successfully found process with ID format: {IdFormat} in processes database", idFormat);
          return process;
        }
      }

      // If still not found, try querying by the raw ID (without prefixes)
      var rawId = cleanProcessId;
      if (rawId.Contains("/"))
      {
        rawId = rawId.Split('/').Last();
      }

      _logger.LogInformation("Trying to query for process by raw ID: {RawId} in processes database", rawId);

      // Try in processes database with a query
      var queryResult = await processesSession.Query<DeduplicationProcess>()
          .Where(p => p.Id.EndsWith(rawId))
          .ToListAsync();

      if (queryResult.Any())
      {
        process = queryResult.First();
        _logger.LogInformation("Found process by query in processes database: {ProcessId}", process.Id);
        return process;
      }

      _logger.LogError("Process with ID {ProcessId} not found after trying all prefix variations in processes database", cleanProcessId);
      throw new Exception($"Process with ID {cleanProcessId} not found");
    }

    public async Task ProcessDeduplicationAsync(string processId)
    {
      // Get the process using our improved GetProcessAsync method
      var process = await GetProcessAsync(processId);

      // Determine which database the process was found in
      string? database = null;
      if (process.Id.StartsWith("processes/"))
      {
        database = "processes";
      }
      else if (process.Id.StartsWith("DeduplicationProcesses/"))
      {
        database = "processes";
      }

      // Initialize steps collection if it doesn't exist
      if (process.Steps == null)
      {
        process.Steps = new List<ProcessStep>();
      }

      // Use concurrency control to update the process status
      try
      {
        process = await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          database ?? "processes", // Use the processes database
          process.Id, // Use the full document ID including the prefix
          async (session, loadedProcess) =>
          {
            // Update process status to "In Processing"
            loadedProcess.Status = "In Processing";
            loadedProcess.ProcessStartDate = DateTime.UtcNow;

            // Initialize steps collection if it doesn't exist
            if (loadedProcess.Steps == null)
            {
              loadedProcess.Steps = new List<ProcessStep>();
            }

            // Add initialization step if not already present
            if (!loadedProcess.Steps.Any(s => s.Name == "Initialization"))
            {
              var initStep = new ProcessStep
              {
                Id = Guid.NewGuid().ToString(),
                Name = "Initialization",
                ProcessId = processId,
                StartDate = DateTime.UtcNow,
                Status = "Completed",
                EndDate = DateTime.UtcNow.AddSeconds(1),
                ProcessedFiles = new List<string>()
              };

              loadedProcess.Steps.Add(initStep);
            }

            _logger.LogInformation("Process {ProcessId} status updated to In Processing", loadedProcess.Id);
            return loadedProcess;
          },
          5 // Maximum number of retries
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to update process {ProcessId} status to In Processing", process.Id);
        throw; // Re-throw to handle in the outer catch block
      }

      // Open a session for the rest of the processing
      using var session = database != null
        ? _context.OpenAsyncSession(database)
        : _context.OpenAsyncSession();

      try
      {
        // Create Insertion step
        var insertionStep = new ProcessStep
        {
          Id = Guid.NewGuid().ToString(),
          Name = "Insertion",
          ProcessId = processId,
          StartDate = DateTime.UtcNow,
          Status = "In Progress",
          ProcessedFiles = new List<string>()
        };

        process.Steps.Add(insertionStep);
        await session.SaveChangesAsync();

        // Get all files from the database for this specific process
        var files = await GetFilesForProcessAsync(processId);

        if (files.Count == 0)
        {
          _logger.LogWarning("No files found for process {ProcessId}", processId);

          // Update process status to Completed using concurrency control
          try
          {
            await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
              database ?? "processes", // Use the processes database
              process.Id, // Use the full document ID including the prefix
              async (noFilesSession, loadedProcess) =>
              {
                loadedProcess.Status = "Completed";
                loadedProcess.ProcessEndDate = DateTime.UtcNow;
                loadedProcess.CompletedAt = DateTime.UtcNow; // Ensure CompletedAt is set
                loadedProcess.CurrentStage = "Completed";
                loadedProcess.ProcessedFiles = 0;
                loadedProcess.CompletionNotes = "Done kamelna.";

                _logger.LogInformation("Process {ProcessId} status updated to Completed (no files)", loadedProcess.Id);
                return loadedProcess;
              },
              5 // Maximum number of retries
            );

            // Synchronize file statuses with the completed process status
            await SynchronizeFileStatusesWithProcessAsync(process.Id, "Completed");
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to update process {ProcessId} status to Completed (no files)", process.Id);
            throw; // Re-throw to handle in the outer catch block
          }

          return;
        }

        foreach (var file in files)
        {
          await ProcessFileInsertionAsync(file, process, insertionStep, session);
        }

        // Complete the Insertion step
        insertionStep.Status = "Completed";
        insertionStep.EndDate = DateTime.UtcNow;
        await session.SaveChangesAsync();

        // Create Identification step
        var identificationStep = new ProcessStep
        {
          Id = Guid.NewGuid().ToString(),
          Name = "Identification",
          ProcessId = processId,
          StartDate = DateTime.UtcNow,
          Status = "In Progress",
          ProcessedFiles = new List<string>()
        };

        process.Steps.Add(identificationStep);
        await session.SaveChangesAsync();

        // Get all files with status "Inserted"
        foreach (var file in files.Where(f => f.Status == "Inserted"))
        {
          await ProcessFileIdentificationAsync(file, process, identificationStep, session);
        }

        // Complete the Identification step
        identificationStep.Status = "Completed";
        identificationStep.EndDate = DateTime.UtcNow;

        // Get counts of duplicate records and exceptions
        var duplicateRecords = await _duplicateRecordService.GetDuplicateRecordsByProcessAsync(process.Id);
        var exceptions = await _exceptionService.GetExceptionsByProcessIdAsync(process.Id);

        // Calculate total processed files
        var processedFileIds = new HashSet<string>();
        foreach (var step in process.Steps)
        {
          if (step.ProcessedFiles != null)
          {
            foreach (var fileId in step.ProcessedFiles)
            {
              processedFileIds.Add(fileId);
            }
          }
        }

        // Update process status using concurrency control
        try
        {
          await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
            database ?? "processes", // Use the processes database
            process.Id, // Use the full document ID including the prefix
            async (updateSession, loadedProcess) =>
            {
              loadedProcess.Status = "Completed";
              loadedProcess.ProcessEndDate = DateTime.UtcNow;
              loadedProcess.CompletedAt = DateTime.UtcNow; // Ensure CompletedAt is set
              loadedProcess.CurrentStage = "Completed";

              // Update the processed files count
              loadedProcess.ProcessedFiles = processedFileIds.Count;

              // Add summary information to completion notes
              loadedProcess.CompletionNotes = $"Process completed successfully. " +
                $"Processed {processedFileIds.Count} files. " +
                $"Found {duplicateRecords.Count} duplicate records with {duplicateRecords.Sum(dr => dr.Duplicates?.Count ?? 0)} total matches. " +
                $"Created {exceptions.Count} exceptions.";

              _logger.LogInformation("Process {ProcessId} status updated to Completed with {ProcessedFiles} files processed",
                loadedProcess.Id, loadedProcess.ProcessedFiles);
              return loadedProcess;
            },
            5 // Maximum number of retries
          );

          // Synchronize file statuses with the completed process
          await SynchronizeFileStatusesWithProcessAsync(process.Id, "Completed");
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to update process {ProcessId} status to Completed", process.Id);
          throw; // Re-throw to handle in the outer catch block
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing deduplication for process {ProcessId}", processId);

        // Update process status to Error using concurrency control
        try
        {
          await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
            database ?? "processes", // Use the processes database
            process.Id, // Use the full document ID including the prefix
            async (errorSession, loadedProcess) =>
            {
              loadedProcess.Status = "Error";

              _logger.LogInformation("Process {ProcessId} status updated to Error", loadedProcess.Id);
              return loadedProcess;
            },
            5 // Maximum number of retries
          );

          // Synchronize file statuses with the error process status
          await SynchronizeFileStatusesWithProcessAsync(process.Id, "Error");
        }
        catch (Exception storeEx)
        {
          _logger.LogError(storeEx, "Failed to update process {ProcessId} status to Error", process.Id);
          // Don't throw here, we want to throw the original exception
        }

        throw;
      }
    }

    private async Task<List<FileModel>> GetFilesForProcessAsync(string processId)
    {
      try
      {
        // First, load the process to get the file IDs using our improved method
        DeduplicationProcess process;
        try
        {
          process = await GetProcessAsync(processId);
          _logger.LogInformation("Successfully found process {ProcessId} with ID format {ProcessIdFormat}",
              processId, process.Id);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Process {ProcessId} not found", processId);
          return new List<FileModel>();
        }

        if (process.FileIds == null || !process.FileIds.Any())
        {
          _logger.LogWarning("Process {ProcessId} has no file IDs", processId);
          return new List<FileModel>();
        }

        _logger.LogInformation("Process {ProcessId} has {FileCount} file IDs", processId, process.FileIds.Count);

        // RavenDB doesn't handle Contains with a collection well, so let's load each file individually
        // This is more efficient for RavenDB than trying to use a Contains expression
        var result = new List<FileModel>();

        using var filesSession = _context.OpenAsyncSession(database: "Files");
        foreach (var fileId in process.FileIds)
        {
          try
          {
            _logger.LogInformation("Attempting to load file with ID {FileId}", fileId);
            var file = await filesSession.LoadAsync<FileModel>(fileId);

            if (file != null)
            {
              _logger.LogInformation("Found file {FileName} with ID {FileId}, Status: {Status}",
                  file.FileName, file.Id, file.Status);

              if (file.Status != "Deleted")
              {
                result.Add(file);
              }
              else
              {
                _logger.LogInformation("File {FileId} is marked as deleted, skipping", fileId);
              }
            }
            else
            {
              _logger.LogWarning("File with ID {FileId} not found in database", fileId);
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error loading file with ID {FileId} for process {ProcessId}", fileId, processId);
          }
        }

        _logger.LogInformation("Found {FileCount} valid files for process {ProcessId}", result.Count, processId);
        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving files for process {ProcessId}", processId);
        return new List<FileModel>();
      }
    }

    // Keep the old method for backward compatibility
    private async Task<List<FileModel>> GetFilesForProcessAsync()
    {
      // In a real implementation, you would filter files by process ID
      // For this example, we'll simply get all files
      using var session = _context.OpenAsyncSession(database: "Files");

      // Use the built-in RavenDB async methods
      var query = session.Query<FileModel>()
          .Where(f => f.Status != "Deleted");

      return await query.ToListAsync();
    }

    private async Task ProcessFileInsertionAsync(FileModel file, DeduplicationProcess process, ProcessStep step, IAsyncDocumentSession session)
    {
      try
      {
        _logger.LogInformation("Processing file insertion for {FileName}", file.FileName);

        // Check for conflicts with existing files
        var existingFiles = await GetFilesForProcessAsync();
        var conflicts = new List<string>();

        foreach (var existingFile in existingFiles.Where(f => f.Id != file.Id && f.Status == "Inserted"))
        {
          // Verify faces using T4FaceService
          var verificationResult = await _t4FaceService.VerifyFacesAsync(file.Base64String, existingFile.Base64String);

          if (verificationResult.IsMatch && verificationResult.Confidence > 0.7) // Threshold can be adjusted
          {
            // Create conflict record
            await _conflictService.CreateConflictAsync(
                process.Id,
                file.FileName,
                $"Conflict with {existingFile.FileName}",
                verificationResult.Confidence);

            conflicts.Add(existingFile.FileName);
          }
        }

        if (conflicts.Count == 0)
        {
          // No conflicts found, insert the face into T4Face
          // Note: In a real implementation, you'd call the actual T4Face API to add the face
          // For now, we'll simulate by updating the file status
          using (var fileSession = _context.OpenAsyncSession(database: "Files"))
          {
            var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
            if (dbFile != null)
            {
              dbFile.Status = "Inserted";
              dbFile.ProcessStatus = "Processing";
              // In a real implementation, you would store the Face ID returned by T4Face
              dbFile.FaceId = Guid.NewGuid().ToString();
              await fileSession.SaveChangesAsync();
            }
          }

          // Add to processed files
          step.ProcessedFiles.Add(file.Id);

          // Update in-memory file for subsequent operations
          file.Status = "Inserted";
        }
        else
        {
          _logger.LogWarning("File {FileName} has conflicts with {ConflictCount} other files", file.FileName, conflicts.Count);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing file insertion for {FileName}", file.FileName);

        // Create exception record
        await _exceptionService.CreateExceptionAsync(
            process.Id,
            file.FileName,
            new List<string> { "Error during insertion phase" },
            0.0);
      }
    }

    private async Task ProcessFileIdentificationAsync(FileModel file, DeduplicationProcess process, ProcessStep step, IAsyncDocumentSession session)
    {
      try
      {
        _logger.LogInformation("Processing file identification for {FileName}", file.FileName);

        // Don't log the base64 string as it's too large
        _logger.LogDebug("Processing identification for file with size: {Size} bytes",
            file.Base64String?.Length ?? 0);

        // Identify the face against all others in the database
        var identificationResult = await _t4FaceService.IdentifyFaceAsync(file.Base64String ?? string.Empty);

        if (identificationResult.Success && identificationResult.HasMatches)
        {
          // Use all matches from the API without filtering
          var matches = identificationResult.Matches.ToList();

          if (matches.Any())
          {
            _logger.LogInformation("File {FileName} matched with {MatchCount} candidates",
                file.FileName, matches.Count);

            // Group matches by person ID to eliminate duplicates
            var uniqueMatches = matches
                .GroupBy(m => m.FaceId)
                .Select(g => g.OrderByDescending(m => m.Confidence).First())
                .OrderByDescending(m => m.Confidence)
                .ToList();

            _logger.LogInformation("After deduplication, file {FileName} has {UniqueCount} unique person matches",
                file.FileName, uniqueMatches.Count);

            // Check if this is a self-match (the file matching with itself)
            // This happens when the file was just registered with T4FACE and then immediately identified
            bool isSelfMatch = false;

            // Check for self-matches regardless of how many matches we have
            // This handles cases where the same face is registered multiple times

            // First, check if all matches have the same person name (indicating potential self-matches)
            bool allSamePersonName = uniqueMatches.Count > 0 &&
                uniqueMatches.All(m => !string.IsNullOrEmpty(m.Name) &&
                                      m.Name == uniqueMatches[0].Name);

            // Also check if all matches have very high confidence (>90%)
            bool allHighConfidence = uniqueMatches.All(m => m.Confidence > 90);

            if (allSamePersonName && allHighConfidence)
            {
              _logger.LogInformation("All matches have the same person name and high confidence. Checking for self-match...");

              // Get the person name from the first match
              string personName = uniqueMatches[0].Name;

              // Check if the person name contains a hash that might match this file
              if (!string.IsNullOrEmpty(personName) && personName.StartsWith("person_"))
              {
                // Extract the hash part from the person name
                string personHash = personName.Substring("person_".Length);

                // Compute a hash for this file's base64 string
                string fileHash = ComputeHash(file.Base64String ?? string.Empty);
                string truncatedFileHash = fileHash.Length >= personHash.Length ?
                    fileHash.Substring(0, personHash.Length) : fileHash;

                // If the hashes match or are very similar, it's likely a self-match
                if (personHash == truncatedFileHash ||
                    (personHash.Length >= 8 && truncatedFileHash.Length >= 8 &&
                     personHash.Substring(0, 8) == truncatedFileHash.Substring(0, 8)))
                {
                  _logger.LogInformation("File {FileName} matched with itself (hash match). Ignoring self-match. Person name: {PersonName}, Hash: {Hash}",
                      file.FileName, personName, personHash);
                  isSelfMatch = true;
                }
                else
                {
                  _logger.LogInformation("Hash comparison: Person hash: {PersonHash}, File hash: {FileHash}",
                      personHash, truncatedFileHash);
                }
              }

              // Also check if any of the matched IDs correspond to the current file's FaceId
              if (!isSelfMatch && !string.IsNullOrEmpty(file.FaceId) && file.FaceId != "")
              {
                foreach (var match in uniqueMatches)
                {
                  if (match.FaceId == file.FaceId)
                  {
                    _logger.LogInformation("File {FileName} matched with itself (same FaceId: {FaceId}). Ignoring self-match.",
                        file.FileName, file.FaceId);
                    isSelfMatch = true;
                    break;
                  }
                }
              }
            }

            // Only proceed with duplicate record creation if this is NOT a self-match
            if (!isSelfMatch)
            {
              // Log only the top 3 matches to avoid huge logs
              var topMatches = uniqueMatches.Take(3).Select(m => new { m.Name, m.Confidence, m.FaceId }).ToList();
              _logger.LogDebug("Top unique matches: {@TopMatches}", topMatches);

              // Extract person IDs from the matches
              var personIds = uniqueMatches.Select(m => m.FaceId).ToList();

              // Try to find the actual files associated with these person IDs
              var filesForPersons = await GetFilesByPersonIdsAsync(personIds);

              // Create a list of candidate file names that are actual file names, not person IDs
              var candidateFileNames = new List<string>();
              foreach (var match in uniqueMatches)
              {
                if (filesForPersons.TryGetValue(match.FaceId, out var fileInfo))
                {
                  candidateFileNames.Add(fileInfo.FileName);
                }
                else
                {
                  // If we can't find a file for this person ID, use the person ID as a fallback
                  candidateFileNames.Add(match.Name);
                }
              }

              // Create an exception record with deduplicated data
              var exception = await _exceptionService.CreateExceptionAsync(
                  process.Id,
                  file.FileName,
                  candidateFileNames,
                  uniqueMatches.First().Confidence,
                  new Dictionary<string, object>
                  {
                    ["matchDetails"] = uniqueMatches.Select(m => new
                    {
                      Name = filesForPersons.TryGetValue(m.FaceId, out var fileInfo) ? fileInfo.FileName : m.Name,
                      Confidence = m.Confidence,
                      PersonId = m.FaceId,
                      FileId = filesForPersons.TryGetValue(m.FaceId, out var fi) ? fi.FileId : string.Empty
                    }).ToList(),
                    ["processingDate"] = DateTime.UtcNow
                  });

              // Create a list of duplicate matches
              var duplicateMatches = uniqueMatches.Select(m => new DuplicateMatch
              {
                FileId = filesForPersons.TryGetValue(m.FaceId, out var fileInfo) ? fileInfo.FileId : string.Empty,
                FileName = filesForPersons.TryGetValue(m.FaceId, out var fi) ? fi.FileName : m.Name,
                Confidence = m.Confidence,
                PersonId = m.FaceId
              }).ToList();

              // Use the DuplicateRecordService to create a duplicate record with proper prefixes
              var duplicatedRecord = await _duplicateRecordService.CreateDuplicateRecordAsync(
                process.Id,
                file.Id,
                file.FileName,
                duplicateMatches
              );

              _logger.LogInformation("Stored duplicate record in dedicated database: {RecordId}", duplicatedRecord.Id);

              _logger.LogWarning("File {FileName} has {MatchCount} potential duplicates", file.FileName, uniqueMatches.Count);
            }
            else
            {
              _logger.LogInformation("No matches found for file {FileName}", file.FileName);
            }
          }
          else if (!identificationResult.Success)
          {
            _logger.LogWarning("Identification failed for file {FileName}: {ErrorMessage}",
                file.FileName, identificationResult.ErrorMessage);
          }

          // Add to processed files
          step.ProcessedFiles.Add(file.Id);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing file identification for {FileName}", file.FileName);

        // Create exception record for the error
        await _exceptionService.CreateExceptionAsync(
            process.Id,
            file.FileName,
            new List<string> { "Error during identification phase" },
            0.0,
            new Dictionary<string, object>
            {
              ["errorMessage"] = ex.Message,
              ["errorType"] = ex.GetType().Name,
              ["processingDate"] = DateTime.UtcNow
            });
      }
    }

    // Helper method to get file IDs by file names
    private async Task<Dictionary<string, string>> GetFileIdsByNamesAsync(List<string> fileNames)
    {
      var result = new Dictionary<string, string>();

      try
      {
        using var session = _context.OpenAsyncSession(database: "Files");

        // Instead of a Contains query, fetch all files and filter in memory
        // This is safer for RavenDB compatibility
        var allFiles = await session.Query<FileModel>().ToListAsync();

        // Filter the files that match our fileNames list
        var matchingFiles = allFiles.Where(f => fileNames.Contains(f.FileName));

        foreach (var file in matchingFiles)
        {
          result[file.FileName] = file.Id;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching file IDs by names");
      }

      return result;
    }

    // Helper method to get files by person IDs
    private async Task<Dictionary<string, FileInfo>> GetFilesByPersonIdsAsync(List<string> personIds)
    {
      var result = new Dictionary<string, FileInfo>();

      try
      {
        using var session = _context.OpenAsyncSession(database: "Files");

        // Fetch all files
        var allFiles = await session.Query<FileModel>().ToListAsync();

        // Group files by their FaceId (which corresponds to PersonId in the T4Face service)
        // This is a simplification - in a real system, you'd have a proper mapping between
        // person IDs in the face recognition system and files in your database
        foreach (var file in allFiles)
        {
          if (!string.IsNullOrEmpty(file.FaceId) && file.FaceId != "" && personIds.Contains(file.FaceId))
          {
            result[file.FaceId] = new FileInfo
            {
              FileId = file.Id,
              FileName = file.FileName,
              FilePath = file.FilePath
            };
          }
        }

        _logger.LogInformation("Found {Count} files matching {PersonIdCount} person IDs",
            result.Count, personIds.Count);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error fetching files by person IDs");
      }

      return result;
    }

    // Helper class to store file information
    private class FileInfo
    {
      public string FileId { get; set; } = string.Empty;
      public string FileName { get; set; } = string.Empty;
      public string FilePath { get; set; } = string.Empty;
    }

    public async Task PauseProcessAsync(string processId)
    {
      try
      {
        // Get the process using our improved GetProcessAsync method
        var process = await GetProcessAsync(processId);
        _logger.LogInformation("Process found for pause: {ProcessId}", process.Id);

        // Always use the processes database
        string database = "processes";

        // Use concurrency control to update the process status
        await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          database, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          async (session, loadedProcess) =>
          {
            // Only pause if the process is in a state that can be paused
            if (loadedProcess.Status == "In Processing" || loadedProcess.Status == "Started")
            {
              loadedProcess.Status = "Paused";
              _logger.LogInformation("Process {ProcessId} status updated to Paused", loadedProcess.Id);
            }
            else
            {
              _logger.LogWarning("Cannot pause process {ProcessId} because it is in {Status} state", loadedProcess.Id, loadedProcess.Status);
              throw new InvalidOperationException($"Cannot pause process in {loadedProcess.Status} state");
            }

            return loadedProcess;
          },
          5 // Maximum number of retries
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error pausing process {ProcessId}", processId);
        throw;
      }
    }

    public async Task ResumeProcessAsync(string processId)
    {
      try
      {
        // Get the process using our improved GetProcessAsync method
        var process = await GetProcessAsync(processId);
        _logger.LogInformation("Process found for resume: {ProcessId}", process.Id);

        // Always use the processes database
        string database = "processes";

        // Use concurrency control to update the process status
        bool canContinueProcessing = false;

        await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          database, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          async (session, loadedProcess) =>
          {
            // Only resume if the process is paused
            if (loadedProcess.Status == "Paused")
            {
              loadedProcess.Status = "In Processing";
              _logger.LogInformation("Process {ProcessId} status updated to In Processing", loadedProcess.Id);
              canContinueProcessing = true;
            }
            else
            {
              _logger.LogWarning("Cannot resume process {ProcessId} because it is in {Status} state", loadedProcess.Id, loadedProcess.Status);
              throw new InvalidOperationException($"Cannot resume process in {loadedProcess.Status} state. Process must be in Paused state to resume.");
            }

            return loadedProcess;
          },
          5 // Maximum number of retries
        );

        // Continue processing if status was successfully updated
        if (canContinueProcessing)
        {
          await ProcessDeduplicationAsync(processId);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error resuming process {ProcessId}", processId);
        throw;
      }
    }

    public async Task CleanupProcessAsync(string processId, string username = null)
    {
      // Get the process using our improved GetProcessAsync method
      var process = await GetProcessAsync(processId);
      _logger.LogInformation("Process found for cleanup: {ProcessId}", process.Id);

      // Always use the processes database
      string database = "processes";

      // Use the provided username or fall back to the process username or system
      var cleanupUsername = username ?? process.Username ?? process.CreatedBy ?? "system";

      // Use concurrency control to update the process status
      process = await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
        database, // Use the processes database
        process.Id, // Use the full document ID including the prefix
        async (session, loadedProcess) =>
        {
          // Update process status to "Cleaning"
          loadedProcess.Status = "Cleaning";
          loadedProcess.CleanupDate = DateTime.UtcNow;
          loadedProcess.CleanupUsername = cleanupUsername;
          loadedProcess.CurrentStage = "Cleaning";

          _logger.LogInformation("Process {ProcessId} status updated to Cleaning by {Username}", loadedProcess.Id, cleanupUsername);
          return loadedProcess;
        },
        5 // Maximum number of retries
      );

      // Open a session for the rest of the processing
      using var session = _context.OpenAsyncSession(database);

      try
      {
        // Get files associated with this specific process
        var files = await GetFilesForProcessAsync(processId);
        _logger.LogInformation("Found {FileCount} files to clean up for process {ProcessId}", files.Count, process.Id);

        int successCount = 0;
        int errorCount = 0;

        foreach (var file in files)
        {
          try
          {
            // Delete from T4Face if we have a FaceId
            if (!string.IsNullOrEmpty(file.FaceId) && file.FaceId != "")
            {
              _logger.LogInformation("Would delete face with ID {FaceId} from T4Face (simulated)", file.FaceId);
              // In a real implementation, you would call T4Face API to delete the face
            }

            // Update file status
            using (var fileSession = _context.OpenAsyncSession(database: "Files"))
            {
              var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
              if (dbFile != null)
              {
                _logger.LogInformation("Updating file {FileName} status to Deleted", dbFile.FileName);
                dbFile.Status = "Deleted";
                dbFile.ProcessStatus = "Completed";
                await fileSession.SaveChangesAsync();
                successCount++;
              }
              else
              {
                _logger.LogWarning("File with ID {FileId} not found in database during cleanup", file.Id);
                errorCount++;
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error cleaning up file {FileName} for process {ProcessId}", file.FileName, processId);
            errorCount++;
          }
        }

        // Update process status to "Cleaned" using concurrency control
        await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          database, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          async (finalSession, loadedProcess) =>
          {
            loadedProcess.Status = "Cleaned";
            loadedProcess.CurrentStage = "Cleaned";
            loadedProcess.CompletionNotes = $"Successfully cleaned up {successCount} files. {errorCount} files had errors.";

            // Ensure CompletedAt is set if it wasn't already
            if (loadedProcess.CompletedAt == null)
            {
              loadedProcess.CompletedAt = loadedProcess.ProcessEndDate ?? DateTime.UtcNow;
            }

            _logger.LogInformation("Process {ProcessId} cleanup completed. Success: {SuccessCount}, Errors: {ErrorCount}",
                loadedProcess.Id, successCount, errorCount);
            return loadedProcess;
          },
          5 // Maximum number of retries
        );

        // Synchronize file statuses with the cleaned process status
        await SynchronizeFileStatusesWithProcessAsync(process.Id, "Cleaned");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cleaning up process {ProcessId}", processId);

        // Update process status to "Error" using concurrency control
        try
        {
          await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
            database, // Use the processes database
            process.Id, // Use the full document ID including the prefix
            async (errorSession, loadedProcess) =>
            {
              loadedProcess.Status = "Error";
              loadedProcess.CompletionNotes = $"Error during cleanup: {ex.Message}";

              _logger.LogInformation("Process {ProcessId} status updated to Error during cleanup", loadedProcess.Id);
              return loadedProcess;
            },
            5 // Maximum number of retries
          );

          // Synchronize file statuses with the error process status
          await SynchronizeFileStatusesWithProcessAsync(process.Id, "Error");
        }
        catch (Exception storeEx)
        {
          _logger.LogError(storeEx, "Failed to update process {ProcessId} status to Error during cleanup", process.Id);
          // Continue with the original exception
        }

        throw;
      }
    }

    // Helper method to compute a hash for a string (similar to the one in T4FaceService)
    private static string ComputeHash(string input)
    {
      if (string.IsNullOrEmpty(input))
      {
        return string.Empty;
      }

      var bytes = System.Text.Encoding.UTF8.GetBytes(input);
      var hash = System.Security.Cryptography.SHA256.HashData(bytes);
      return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Synchronizes file statuses with the process status to ensure consistency
    /// </summary>
    /// <param name="processId">The ID of the process</param>
    /// <param name="processStatus">The current status of the process</param>
    private async Task SynchronizeFileStatusesWithProcessAsync(string processId, string processStatus)
    {
      try
      {
        _logger.LogInformation("Synchronizing file statuses for process {ProcessId} with status {Status}", processId, processStatus);

        // Get all files for this process
        var files = await GetFilesForProcessAsync(processId);
        if (files.Count == 0)
        {
          _logger.LogInformation("No files found to synchronize for process {ProcessId}", processId);
          return;
        }

        int updatedCount = 0;

        // Update file statuses based on process status
        using var fileSession = _context.OpenAsyncSession(database: "Files");
        foreach (var file in files)
        {
          var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
          if (dbFile == null)
          {
            _logger.LogWarning("File with ID {FileId} not found during status synchronization", file.Id);
            continue;
          }

          bool needsUpdate = false;

          // Set appropriate file status based on process status
          if (processStatus == "Completed")
          {
            // If process is completed but file still shows as "Uploaded" or in processing state
            if (dbFile.Status == "Uploaded" || dbFile.ProcessStatus == "Processing")
            {
              dbFile.Status = "Inserted";
              dbFile.ProcessStatus = "Completed";
              needsUpdate = true;
            }
          }
          else if (processStatus == "Cleaned")
          {
            // If process is cleaned but file is not marked as deleted
            if (dbFile.Status != "Deleted")
            {
              dbFile.Status = "Deleted";
              dbFile.ProcessStatus = "Completed";
              needsUpdate = true;
            }
          }
          else if (processStatus == "Error")
          {
            // If process has error, mark file process status accordingly
            if (dbFile.ProcessStatus != "Failed")
            {
              dbFile.ProcessStatus = "Failed";
              needsUpdate = true;
            }
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
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error synchronizing file statuses for process {ProcessId}", processId);
        // Don't throw the exception - this is a background operation that shouldn't fail the main process
      }
    }
  }
}
