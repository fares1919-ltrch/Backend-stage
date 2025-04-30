using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Interfaces;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Data;
using Microsoft.Extensions.Logging;

namespace Backend.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  [Authorize]
  public class DeduplicationController : ControllerBase
  {
    private readonly IDeduplicationService _deduplicationService;
    private readonly ILogger<DeduplicationController> _logger;
    private readonly RavenDbContext _context;

    public DeduplicationController(
        IDeduplicationService deduplicationService,
        ILogger<DeduplicationController> logger,
        RavenDbContext context)
    {
      _deduplicationService = deduplicationService ?? throw new ArgumentNullException(nameof(deduplicationService));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Starts a new deduplication process
    /// </summary>
    /// <returns>The created deduplication process</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(DeduplicationProcess), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> StartDeduplication()
    {
      try
      {
        // Get the current user's email or username
        var username = User.Identity?.Name ?? "anonymous";

        var process = await _deduplicationService.StartDeduplicationProcessAsync(username);

        // Return detailed response
        return Ok(new
        {
          success = true,
          message = "New deduplication process created successfully",
          processId = process.Id,
          name = process.Name,
          status = process.Status,
          createdAt = process.CreatedAt,
          username = process.Username,
          createdBy = process.CreatedBy,
          note = "This process has no files associated with it yet. Use POST /api/Deduplication/start-process to create a process with files."
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error starting deduplication process");
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Starts a new deduplication process with specific files
    /// </summary>
    /// <param name="request">The deduplication process request</param>
    /// <returns>The created deduplication process</returns>
    [HttpPost("start-process")]
    [ProducesResponseType(typeof(DeduplicationProcess), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> StartProcess([FromBody] DeduplicationProcessDto request)
    {
      try
      {
        // Get the current user's email or username
        var username = User.Identity?.Name ?? "anonymous";

        var process = await _deduplicationService.StartProcessAsync(request, username);

        // Get file count information
        var totalFiles = process.FileIds?.Count ?? 0;

        // Return detailed response
        return Ok(new
        {
          success = true,
          message = "Deduplication process created successfully",
          processId = process.Id,
          name = process.Name,
          status = process.Status,
          createdAt = process.CreatedAt,
          username = process.Username,
          createdBy = process.CreatedBy,
          totalFiles = totalFiles,
          fileIds = process.FileIds,
          nextStep = "Use POST /api/Deduplication/process/{processId} to start processing"
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error starting specific deduplication process");
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Gets all deduplication processes
    /// </summary>
    /// <returns>List of all processes</returns>
    [HttpGet("processes")]
    [ProducesResponseType(typeof(List<ProcessDTO>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetAllProcesses()
    {
      try
      {
        var processes = await _deduplicationService.GetAllProcesses();
        return Ok(processes);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting all processes");
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Gets details about a specific deduplication process
    /// </summary>
    /// <param name="processId">The ID of the process to retrieve</param>
    /// <returns>The deduplication process details</returns>
    [HttpGet("process/{processId}")]
    [ProducesResponseType(typeof(DeduplicationProcess), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetProcess(string processId)
    {
      try
      {
        _logger.LogInformation("Getting details for process ID: {ProcessId}", processId);

        if (string.IsNullOrEmpty(processId))
        {
          return BadRequest(new { message = "Process ID is required" });
        }

        var process = await _deduplicationService.GetProcessAsync(processId);

        // If the process is completed, check for and fix inconsistencies
        if (process.Status == "Completed")
        {
          var statusSyncService = HttpContext.RequestServices.GetService<IStatusSynchronizationService>();
          if (statusSyncService != null)
          {
            _logger.LogInformation("Fixing process data for {ProcessId}", processId);
            await statusSyncService.FixProcessDataAsync(processId);

            // Reload the process to get the updated data
            process = await _deduplicationService.GetProcessAsync(processId);
          }
        }

        // Get duplicate records for this process
        var duplicateRecordService = HttpContext.RequestServices.GetService<DuplicateRecordService>();
        var exceptionService = HttpContext.RequestServices.GetService<ExceptionService>();

        // Get duplicate records and exceptions in parallel
        var duplicateRecordsTask = duplicateRecordService?.GetDuplicateRecordsByProcessAsync(processId);
        var exceptionsTask = exceptionService?.GetExceptionsByProcessIdAsync(processId);

        // Wait for both tasks to complete
        await Task.WhenAll(
            duplicateRecordsTask ?? Task.FromResult<List<DuplicatedRecord>>(new List<DuplicatedRecord>()),
            exceptionsTask ?? Task.FromResult<List<DeduplicationException>>(new List<DeduplicationException>())
        );

        // Get results
        var duplicateRecords = duplicateRecordsTask?.Result ?? new List<DuplicatedRecord>();
        var exceptions = exceptionsTask?.Result ?? new List<DeduplicationException>();

        // Calculate processed files count more accurately
        var processedFilesCount = 0;
        var allProcessedFileIds = new HashSet<string>();

        // Count processed files from steps
        if (process.Steps != null)
        {
          foreach (var step in process.Steps)
          {
            if (step.ProcessedFiles != null)
            {
              foreach (var fileId in step.ProcessedFiles)
              {
                allProcessedFileIds.Add(fileId);
              }
            }
          }
          processedFilesCount = allProcessedFileIds.Count;
        }

        // If no steps are recorded but we have duplicate records, use those to determine processed files
        if (processedFilesCount == 0 && duplicateRecords.Any())
        {
          processedFilesCount = duplicateRecords.Select(dr => dr.OriginalFileId).Distinct().Count();
        }

        // Create a summary of processing results
        var processingResults = new
        {
          DuplicateRecords = new
          {
            Total = duplicateRecords.Count,
            ByStatus = new
            {
              Detected = duplicateRecords.Count(dr => dr.Status == "Detected"),
              Confirmed = duplicateRecords.Count(dr => dr.Status == "Confirmed"),
              Rejected = duplicateRecords.Count(dr => dr.Status == "Rejected")
            },
            TotalMatches = duplicateRecords.Sum(dr => dr.Duplicates?.Count ?? 0)
          },
          Exceptions = new
          {
            Total = exceptions.Count,
            ByStatus = new
            {
              Pending = exceptions.Count(e => e.Status == "Pending"),
              Reviewed = exceptions.Count(e => e.Status == "Reviewed"),
              Confirmed = exceptions.Count(e => e.Status == "Confirmed"),
              Rejected = exceptions.Count(e => e.Status == "Rejected")
            }
          }
        };

        // Create steps if they don't exist but we have processing data
        var steps = process.Steps ?? new List<ProcessStep>();
        if (!steps.Any() && (duplicateRecords.Any() || exceptions.Any()))
        {
          // Create synthetic steps based on the data we have
          if (process.ProcessStartDate.HasValue)
          {
            // Add initialization step
            steps.Add(new ProcessStep
            {
              Id = Guid.NewGuid().ToString(),
              Name = "Initialization",
              Status = "Completed",
              StartDate = process.ProcessStartDate.Value,
              EndDate = process.ProcessStartDate.Value.AddSeconds(1),
              ProcessedFiles = new List<string>()
            });

            // Add file processing step
            var fileProcessingStep = new ProcessStep
            {
              Id = Guid.NewGuid().ToString(),
              Name = "File Processing",
              Status = "Completed",
              StartDate = process.ProcessStartDate.Value.AddSeconds(1),
              ProcessedFiles = duplicateRecords.Select(dr => dr.OriginalFileId).Distinct().ToList()
            };

            // Set end date based on process end date or last duplicate record
            if (process.ProcessEndDate.HasValue)
            {
              fileProcessingStep.EndDate = process.ProcessEndDate.Value;
            }
            else if (duplicateRecords.Any())
            {
              fileProcessingStep.EndDate = duplicateRecords.Max(dr => dr.DetectedDate);
            }

            steps.Add(fileProcessingStep);
          }
        }

        // Use the ProcessedFiles property from the model if it's set, otherwise calculate it
        int processedFiles = process.ProcessedFiles;
        if (processedFiles == 0 && process.Status == "Completed")
        {
          // If the process is completed but ProcessedFiles is 0, use the calculated count
          processedFiles = processedFilesCount;

          // Update the process in the database to fix the inconsistency
          await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
              "processes", // Use the processes database
              process.Id, // Use the full document ID including the prefix
              async (updateSession, loadedProcess) =>
              {
                loadedProcess.ProcessedFiles = processedFilesCount;
                _logger.LogInformation("Updated ProcessedFiles count for process {ProcessId} from 0 to {ProcessedFiles}",
                      loadedProcess.Id, processedFilesCount);
                return loadedProcess;
              },
              5 // Maximum number of retries
          );
        }

        var response = new
        {
          process.Id,
          process.Name,
          process.Status,
          process.CreatedAt,
          process.ProcessStartDate,
          process.ProcessEndDate,
          process.CleanupDate,
          process.Username,
          TotalFiles = process.FileIds?.Count ?? 0,
          ProcessedFiles = processedFiles,
          Steps = steps,
          process.CompletionNotes,
          ProcessingResults = processingResults,
          DuplicateRecordIds = duplicateRecords.Select(dr => dr.Id).ToList(),
          ExceptionIds = exceptions.Select(e => e.Id).ToList()
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving process {ProcessId}", processId);

        if (ex.Message.Contains("not found"))
        {
          return NotFound(new { message = ex.Message });
        }

        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Processes a deduplication
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <returns>Success message</returns>
    [HttpPost("process/{processId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ProcessDeduplication(string processId)
    {
      _logger.LogInformation("Starting deduplication process for process ID: {ProcessId}", processId);

      if (string.IsNullOrEmpty(processId))
      {
        return BadRequest(new { message = "Process ID is required" });
      }

      try
      {
        // Get the current process to check its status
        var process = await _deduplicationService.GetProcessAsync(processId);

        // Check if the process can be started
        if (process.Status == "In Processing")
        {
          _logger.LogWarning("Cannot start process {ProcessId} because it is already running", processId);

          // Create an exception record
          var exceptionService = HttpContext.RequestServices.GetService<ExceptionService>();
          if (exceptionService != null)
          {
            await exceptionService.CreateExceptionAsync(
                processId,
                "Process Status Conflict",
                new List<string> { "Cannot start a process that is already running" },
                0.0,
                new Dictionary<string, object>
                {
                  ["attemptedAction"] = "Start",
                  ["currentStatus"] = process.Status,
                  ["timestamp"] = DateTime.UtcNow
                });
          }

          return BadRequest(new
          {
            success = false,
            message = "This process is already running. Please wait for it to complete or pause it first.",
            currentStatus = process.Status
          });
        }

        if (process.Status == "Completed" || process.Status == "Cleaned")
        {
          _logger.LogWarning("Cannot start process {ProcessId} because it is already completed", processId);

          // Create an exception record
          var exceptionService = HttpContext.RequestServices.GetService<ExceptionService>();
          if (exceptionService != null)
          {
            await exceptionService.CreateExceptionAsync(
                processId,
                "Process Status Conflict",
                new List<string> { "Cannot start a process that is already completed" },
                0.0,
                new Dictionary<string, object>
                {
                  ["attemptedAction"] = "Start",
                  ["currentStatus"] = process.Status,
                  ["timestamp"] = DateTime.UtcNow
                });
          }

          return BadRequest(new
          {
            success = false,
            message = "This process is already completed and cannot be started again.",
            currentStatus = process.Status
          });
        }

        // Process the deduplication
        await _deduplicationService.ProcessDeduplicationAsync(processId);
        _logger.LogInformation("Deduplication process started successfully for process ID: {ProcessId}", processId);

        // Get updated process information after starting
        var updatedProcess = await _deduplicationService.GetProcessAsync(processId);

        // Get file count information
        var totalFiles = updatedProcess.FileIds?.Count ?? 0;

        // If the process is completed, check for and fix inconsistencies
        if (updatedProcess.Status == "Completed")
        {
          var statusSyncService = HttpContext.RequestServices.GetService<IStatusSynchronizationService>();
          if (statusSyncService != null)
          {
            _logger.LogInformation("Fixing process data for {ProcessId}", processId);
            await statusSyncService.FixProcessDataAsync(processId);

            // Reload the process to get the updated data
            updatedProcess = await _deduplicationService.GetProcessAsync(processId);
          }
        }

        // Return detailed response
        return Ok(new
        {
          success = true,
          message = "Deduplication process started successfully",
          processId = updatedProcess.Id,
          status = updatedProcess.Status,
          startedAt = updatedProcess.ProcessStartDate,
          totalFiles = totalFiles,
          processedFiles = updatedProcess.ProcessedFiles,
          steps = updatedProcess.Steps?.Select(s => new
          {
            s.Name,
            s.Status,
            s.StartDate,
            s.EndDate,
            FilesCount = s.ProcessedFiles?.Count ?? 0
          }).ToList()
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing deduplication for process ID: {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Pauses a deduplication process
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <returns>Success message</returns>
    [HttpPost("pause/{processId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PauseProcess(string processId)
    {
      try
      {
        _logger.LogInformation("Starting pause for process ID: {ProcessId}", processId);

        if (string.IsNullOrEmpty(processId))
        {
          return BadRequest(new { message = "Process ID is required" });
        }

        // Get the current process to check its status
        DeduplicationProcess process;
        try
        {
          process = await _deduplicationService.GetProcessAsync(processId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Process {ProcessId} not found during pause", processId);
          return NotFound(new { message = $"Process with ID {processId} not found" });
        }

        await _deduplicationService.PauseProcessAsync(processId);
        return Ok(new
        {
          message = "Process paused successfully",
          processId = process.Id,
          status = "Paused"
        });
      }
      catch (InvalidOperationException ex)
      {
        _logger.LogWarning(ex, "Invalid operation when pausing process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error pausing process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Resumes a paused deduplication process
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <returns>Success message</returns>
    [HttpPost("resume/{processId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResumeProcess(string processId)
    {
      try
      {
        _logger.LogInformation("Starting resume for process ID: {ProcessId}", processId);

        if (string.IsNullOrEmpty(processId))
        {
          return BadRequest(new { message = "Process ID is required" });
        }

        // Get the current process to check its status
        DeduplicationProcess process;
        try
        {
          process = await _deduplicationService.GetProcessAsync(processId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Process {ProcessId} not found during resume", processId);
          return NotFound(new { message = $"Process with ID {processId} not found" });
        }

        await _deduplicationService.ResumeProcessAsync(processId);
        return Ok(new
        {
          message = "Process resumed successfully",
          processId = process.Id,
          status = "In Processing"
        });
      }
      catch (InvalidOperationException ex)
      {
        _logger.LogWarning(ex, "Invalid operation when resuming process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error resuming process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Cleans up a deduplication process
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <returns>Success message</returns>
    [HttpPost("cleanup/{processId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CleanupProcess(string processId)
    {
      try
      {
        _logger.LogInformation("Starting cleanup for process ID: {ProcessId}", processId);

        if (string.IsNullOrEmpty(processId))
        {
          return BadRequest(new { message = "Process ID is required" });
        }

        // Get the current user's email or username
        var username = User.Identity?.Name ?? "anonymous";

        // Get the current process to check its status
        DeduplicationProcess process;
        try
        {
          process = await _deduplicationService.GetProcessAsync(processId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Process {ProcessId} not found during cleanup", processId);
          return NotFound(new { message = $"Process with ID {processId} not found" });
        }

        // Check if the process is in a state that can be cleaned up
        if (process.Status == "Cleaning" || process.Status == "Cleaned")
        {
          return BadRequest(new { message = $"Process is already in {process.Status} state" });
        }

        await _deduplicationService.CleanupProcessAsync(processId, username);

        // Get the updated process
        var updatedProcess = await _deduplicationService.GetProcessAsync(processId);

        return Ok(new
        {
          message = "Process cleaned up successfully",
          processId = updatedProcess.Id,
          status = updatedProcess.Status,
          cleanupDate = updatedProcess.CleanupDate,
          cleanupUsername = updatedProcess.CleanupUsername,
          completionNotes = updatedProcess.CompletionNotes
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cleaning up process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Gets all files for a specific process
    /// </summary>
    /// <param name="processId">The ID of the process to get files for</param>
    /// <returns>List of files associated with the process</returns>
    [HttpGet("process/{processId}/files")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetProcessFiles(string processId)
    {
      try
      {
        _logger.LogInformation("Getting files for process ID: {ProcessId}", processId);

        if (string.IsNullOrEmpty(processId))
        {
          return BadRequest(new { message = "Process ID is required" });
        }

        // First, verify the process exists
        try
        {
          await _deduplicationService.GetProcessAsync(processId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Process {ProcessId} not found when getting files", processId);
          return NotFound(new { message = $"Process with ID {processId} not found" });
        }

        // Get files for the process
        var files = await GetFilesForProcessAsync(processId);
        return Ok(files);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting files for process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
    }

    private async Task<List<Files.Models.FileModel>> GetFilesForProcessAsync(string processId)
    {
      try
      {
        // First, load the process to get the file IDs
        var process = await _deduplicationService.GetProcessAsync(processId);

        if (process.FileIds == null || !process.FileIds.Any())
        {
          _logger.LogWarning("Process {ProcessId} has no file IDs", processId);
          return new List<Files.Models.FileModel>();
        }

        _logger.LogInformation("Process {ProcessId} has {FileCount} file IDs", processId, process.FileIds.Count);

        // Load each file individually
        var result = new List<Files.Models.FileModel>();

        using var filesSession = _context.OpenSession(database: "Files");
        foreach (var fileId in process.FileIds)
        {
          try
          {
            // Ensure the fileId has the correct format
            string formattedFileId = fileId;
            if (!fileId.StartsWith("files/") && !fileId.Contains("/"))
            {
              formattedFileId = $"files/{fileId}";
              _logger.LogInformation("Reformatted file ID from {OriginalId} to {FormattedId}", fileId, formattedFileId);
            }

            _logger.LogInformation("Attempting to load file with ID {FileId}", formattedFileId);
            var file = filesSession.Load<Files.Models.FileModel>(formattedFileId);

            if (file != null)
            {
              _logger.LogInformation("Found file {FileName} with ID {FileId}, Status: {Status}",
                  file.FileName, file.Id, file.Status);

              if (file.Status != "Deleted")
              {
                // Add Base64 data for frontend display
                if (string.IsNullOrEmpty(file.Base64String) && !string.IsNullOrEmpty(file.FilePath))
                {
                  try
                  {
                    if (System.IO.File.Exists(file.FilePath))
                    {
                      byte[] fileBytes = System.IO.File.ReadAllBytes(file.FilePath);
                      string base64 = Convert.ToBase64String(fileBytes);
                      string mimeType = GetMimeType(file.FileName);
                      file.Base64String = $"data:{mimeType};base64,{base64}";
                    }
                  }
                  catch (Exception ex)
                  {
                    _logger.LogError(ex, "Error loading file data for {FileId}", file.Id);
                  }
                }

                result.Add(file);
              }
              else
              {
                _logger.LogInformation("File {FileId} is marked as deleted, skipping", formattedFileId);
              }
            }
            else
            {
              _logger.LogWarning("File with ID {FileId} not found in database", formattedFileId);
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
        return new List<Files.Models.FileModel>();
      }
    }

    private string GetMimeType(string fileName)
    {
      string extension = Path.GetExtension(fileName).ToLowerInvariant();
      return extension switch
      {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
      };
    }
  }
}
