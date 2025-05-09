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
    private readonly ExceptionService _exceptionService;
    private readonly DuplicateRecordService _duplicateRecordService;
    private readonly ILogger<DeduplicationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _tempFilePath;

    public DeduplicationService(
        RavenDbContext context,
        IT4FaceService t4FaceService,
        ExceptionService exceptionService,
        DuplicateRecordService duplicateRecordService,
        ILogger<DeduplicationService> logger,
        IConfiguration configuration)
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _t4FaceService = t4FaceService ?? throw new ArgumentNullException(nameof(t4FaceService));
      _exceptionService = exceptionService ?? throw new ArgumentNullException(nameof(exceptionService));
      _duplicateRecordService = duplicateRecordService ?? throw new ArgumentNullException(nameof(duplicateRecordService));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
      _tempFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles");

      // No identification threshold - we'll use the raw similarity values from the API

      Directory.CreateDirectory(_tempFilePath);
    }

    public async Task<DeduplicationProcess> StartDeduplicationProcessAsync(string? username = null)
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

    public async Task<DeduplicationProcess> StartProcessAsync(DeduplicationProcessDto request, string? username = null)
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

      // Initialize steps collection if it doesn't exist
      if (process.Steps == null)
      {
        process.Steps = new List<ProcessStep>();
      }

      // Use concurrency control to update the process status
      try
      {
        process = await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          RavenDbContext.DatabaseType.Processes, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          (session, loadedProcess) =>
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
            return Task.FromResult(loadedProcess);
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
      using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);

      try
      {
        // Create a single Face Processing step that combines identification, verification, and registration
        var processingStep = new ProcessStep
        {
          Id = Guid.NewGuid().ToString(),
          Name = "Face Processing",
          ProcessId = processId,
          StartDate = DateTime.UtcNow,
          Status = "In Progress",
          ProcessedFiles = new List<string>()
        };

        process.Steps.Add(processingStep);
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
              RavenDbContext.DatabaseType.Processes, // Use the processes database
              process.Id, // Use the full document ID including the prefix
              (noFilesSession, loadedProcess) =>
              {
                loadedProcess.Status = "Completed";
                loadedProcess.ProcessEndDate = DateTime.UtcNow;
                loadedProcess.CompletedAt = DateTime.UtcNow; // Ensure CompletedAt is set
                loadedProcess.CurrentStage = "Completed";
                loadedProcess.ProcessedFiles = 0;
                loadedProcess.CompletionNotes = "No files to process.";

                _logger.LogInformation("Process {ProcessId} status updated to Completed (no files)", loadedProcess.Id);
                return Task.FromResult(loadedProcess);
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

        // Process each file using our improved method
        var processedFileIds = new HashSet<string>();
        int fileCounter = 0;
        int totalFiles = files.Count;

        foreach (var file in files)
        {
          fileCounter++;
          _logger.LogInformation("Processing file {FileCounter} of {TotalFiles}: {FileName} (ID: {FileId})",
              fileCounter, totalFiles, file.FileName, file.Id);

          var startTime = DateTime.UtcNow;
          await ProcessFileInsertionAsync(file, process, processingStep, session);
          var processingTime = DateTime.UtcNow - startTime;

          _logger.LogInformation("Completed processing file {FileCounter} of {TotalFiles}: {FileName} in {ProcessingTime} seconds",
              fileCounter, totalFiles, file.FileName, processingTime.TotalSeconds);

          processedFileIds.Add(file.Id);
        }

        _logger.LogInformation("Finished processing all {TotalFiles} files for process {ProcessId}",
            totalFiles, process.Id);

        // Complete the processing step
        processingStep.Status = "Completed";
        processingStep.EndDate = DateTime.UtcNow;
        await session.SaveChangesAsync();

        // Get counts of duplicate records and exceptions
        var duplicateRecords = await _duplicateRecordService.GetDuplicateRecordsByProcessAsync(process.Id);
        var exceptions = await _exceptionService.GetExceptionsByProcessIdAsync(process.Id);

        // Update process status using concurrency control
        try
        {
          await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
            RavenDbContext.DatabaseType.Processes, // Use the processes database
            process.Id, // Use the full document ID including the prefix
            (updateSession, loadedProcess) =>
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
              return Task.FromResult(loadedProcess);
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
            RavenDbContext.DatabaseType.Processes, // Use the processes database
            process.Id, // Use the full document ID including the prefix
            (errorSession, loadedProcess) =>
            {
              loadedProcess.Status = "Error";
              loadedProcess.CompletionNotes = $"Error during processing: {ex.Message}";

              _logger.LogInformation("Process {ProcessId} status updated to Error", loadedProcess.Id);
              return Task.FromResult(loadedProcess);
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

        using var filesSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);
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
      using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);

      // Use the built-in RavenDB async methods
      var query = session.Query<FileModel>()
          .Where(f => f.Status != "Deleted");

      return await query.ToListAsync();
    }

    private async Task ProcessFileInsertionAsync(FileModel file, DeduplicationProcess process, ProcessStep step, IAsyncDocumentSession session)
    {
      try
      {
        _logger.LogInformation("Processing file: {FileName}", file.FileName);

        // STEP 1: Generate a consistent person name for this face
        // This will be used for both verification and registration if needed
        string personName = $"person_{ComputeHash(file.Base64String)[..10]}";
        _logger.LogInformation("Generated person name for face: {PersonName}", personName);

        // STEP 2: VERIFY - Check if this face already exists in T4Face with this name
        _logger.LogInformation("STEP 2: Verifying if face already exists with name: {PersonName}", personName);

        var verifyStartTime = DateTime.UtcNow;
        var (faceExists, compareResult) = await CheckFaceExistsAsync(file.Base64String, personName);
        var verifyDuration = DateTime.UtcNow - verifyStartTime;

        _logger.LogInformation("Verification completed in {Duration} seconds with result: {Result}",
            verifyDuration.TotalSeconds, compareResult);

        if (compareResult == "HIT")
        {
          // Face exists according to verify_64 - identify it to find matches
          _logger.LogInformation("Verification returned HIT. Face exists in T4Face. Identifying to find matches.");

          // STEP 2: IDENTIFY - If verification found a match, identify the face to find all matches
          _logger.LogInformation("STEP 2: Identifying face against existing faces in T4Face");

          var identifyStartTime = DateTime.UtcNow;
          var identifyResult = await _t4FaceService.IdentifyFaceAsync(file.Base64String);
          var identifyDuration = DateTime.UtcNow - identifyStartTime;

          if (identifyResult.Success && identifyResult.HasMatches)
          {
            // Filter matches above our threshold (70%)
            var significantMatches = identifyResult.Matches
                .Where(m => m.Confidence > 70) // Using 70% threshold consistently
                .OrderByDescending(m => m.Confidence)
                .ToList();

            if (significantMatches.Count > 0)
            {
              // We found matches above threshold
              var bestMatch = significantMatches.First();

              _logger.LogInformation("Found {MatchCount} matches above 70% threshold.",
                  significantMatches.Count);

              // Update file with the existing face ID from the best match
              using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
              {
                var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
                if (dbFile != null)
                {
                  dbFile.Status = "Inserted";
                  dbFile.ProcessStatus = "Processing";
                  dbFile.FaceId = bestMatch.Name; // Use the existing face ID
                  await fileSession.SaveChangesAsync();
                }
              }

              // Update in-memory file
              file.Status = "Inserted";
              file.FaceId = bestMatch.Name;

              _logger.LogInformation("File updated with existing FaceId: {FaceId}", bestMatch.Name);

              // Process matches to create duplicate records
              await ProcessMatchesAsync(file, process, step, significantMatches);
              return;
            }
            else
            {
              _logger.LogWarning("Verification returned HIT but no significant matches found above 70% threshold. Using first match if available.");

              // If we have any matches at all, use the first one even if below threshold
              if (identifyResult.Matches.Count > 0)
              {
                var bestMatch = identifyResult.Matches.OrderByDescending(m => m.Confidence).First();

                // Update file with the existing face ID
                using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
                {
                  var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
                  if (dbFile != null)
                  {
                    dbFile.Status = "Inserted";
                    dbFile.ProcessStatus = "Processing";
                    dbFile.FaceId = bestMatch.Name;
                    await fileSession.SaveChangesAsync();
                  }
                }

                // Update in-memory file
                file.Status = "Inserted";
                file.FaceId = bestMatch.Name;

                _logger.LogInformation("File updated with existing FaceId: {FaceId} (confidence: {Confidence}%)",
                    bestMatch.Name, bestMatch.Confidence);

                // Process matches to create duplicate records
                await ProcessMatchesAsync(file, process, step, new List<IdentificationMatch> { bestMatch });
                return;
              }
              else
              {
                // Inconsistent state - verification says face exists but identification finds no matches
                _logger.LogWarning("Inconsistent state: Verification says face exists but identification finds no matches. Registering face with name: {PersonName}", personName);
                await RegisterAndIdentifyFaceAsync(file, process, step, personName);
              }
            }
          }
          else
          {
            // Identification failed but verification succeeded - inconsistent state
            _logger.LogWarning("Inconsistent state: Verification says face exists but identification failed. Registering face with name: {PersonName}", personName);
            await RegisterAndIdentifyFaceAsync(file, process, step, personName);
          }
        }
        else if (compareResult == "NO_HIT" || compareResult == "False")
        {
          // Face doesn't exist according to verify_64 - register it
          // Note: "False" is also treated as NO_HIT because that's what the T4Face API returns when verification fails
          _logger.LogInformation("Verification returned {Result}. Face doesn't exist in T4Face. Registering face with name: {PersonName}", compareResult, personName);
          await RegisterAndIdentifyFaceAsync(file, process, step, personName);
        }
        else
        {
          // Unexpected verification result - fall back to registration
          _logger.LogWarning("Unexpected verification result: {Result}. Registering face with name: {PersonName}", compareResult, personName);
          await RegisterAndIdentifyFaceAsync(file, process, step, personName);
        }

        // Ensure the file is marked as processed in the step
        if (!step.ProcessedFiles.Contains(file.Id))
        {
          step.ProcessedFiles.Add(file.Id);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing file: {FileName}", file.FileName);

        // Create exception record
        await _exceptionService.CreateExceptionAsync(
            process.Id,
            file.FileName,
            new List<string> { "Error during processing" },
            0.0,
            new Dictionary<string, object>
            {
              ["errorMessage"] = ex.Message,
              ["errorType"] = ex.GetType().Name,
              ["processingDate"] = DateTime.UtcNow
            }
        );

        // Update file status to error
        try
        {
          using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
          {
            var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
            if (dbFile != null)
            {
              dbFile.Status = "Error";
              dbFile.ProcessStatus = $"Error: Processing failed";
              await fileSession.SaveChangesAsync();
            }
          }
        }
        catch (Exception updateEx)
        {
          _logger.LogError(updateEx, "Error updating file status after processing error: {FileName}", file.FileName);
        }
      }
    }

    // Note: The GetReferencePersonNameAsync method has been removed as we now generate
    // a consistent person name for each face and use it for both verification and registration

    // Helper method to check if a face already exists in T4Face using verify_64
    private async Task<(bool Exists, string CompareResult)> CheckFaceExistsAsync(string base64Image, string referenceFaceId)
    {
      try
      {
        if (string.IsNullOrEmpty(referenceFaceId))
        {
          _logger.LogWarning("Cannot verify face: referenceFaceId is null or empty");
          return (false, "NO_HIT");
        }

        _logger.LogInformation("Verifying face against reference ID: {ReferenceId}", referenceFaceId);

        // Use verify_64 to check if the face exists
        var verificationResult = await _t4FaceService.VerifyFaceAgainstPersonAsync(
            base64Image ?? string.Empty,
            referenceFaceId);

        _logger.LogInformation("Verification result: {CompareResult} with confidence {Confidence}, Success={Success}, Message={Message}",
            verificationResult.CompareResult, verificationResult.Confidence, verificationResult.Success, verificationResult.Message);

        // Handle different verification results
        if (verificationResult.CompareResult == "HIT")
        {
          // Clear HIT result - face exists in the system
          return (true, "HIT");
        }
        else if (verificationResult.CompareResult == "NO_HIT")
        {
          // Clear NO_HIT result - face doesn't exist in the system
          return (false, "NO_HIT");
        }
        else if (!verificationResult.Success && verificationResult.Message.Contains("There is no user with name"))
        {
          // Special case: The reference person doesn't exist in T4Face
          // This is effectively a NO_HIT result
          _logger.LogInformation("Reference person doesn't exist in T4Face. Treating as NO_HIT.");
          return (false, "NO_HIT");
        }
        else
        {
          // Any other result (including "False") - return as is
          return (false, verificationResult.CompareResult);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error checking if face exists in T4Face");
        return (false, "ERROR");
      }
    }

    // Note: The RegisterFirstFaceAsync method has been removed as we now use RegisterAndIdentifyFaceAsync for all cases
    // This ensures a consistent flow where we always verify first, then register if needed

    // Helper method to process matches and create duplicate records
    private async Task ProcessMatchesAsync(FileModel file, DeduplicationProcess process, ProcessStep step, List<IdentificationMatch> matches)
    {
      _logger.LogInformation("Processing {MatchCount} matches for file: {FileName}", matches.Count, file.FileName);

      // Create duplicate matches list directly from the matches
      List<DuplicateMatch> duplicateMatches = new List<DuplicateMatch>();

      // Create duplicate matches for all matches from T4Face
      foreach (var match in matches)
      {
        // Create a duplicate match entry for each match
        duplicateMatches.Add(new DuplicateMatch
        {
          FileId = file.Id, // Use the current file ID
          FileName = file.FileName,
          Confidence = match.Confidence,
          PersonId = match.PersonId // Use the person ID from T4Face
        });

        _logger.LogInformation("Created duplicate match for person: {PersonName} (ID: {PersonId}) with confidence {Confidence}%",
            match.Name, match.PersonId, match.Confidence);
      }

      // Create duplicate record in the database
      if (duplicateMatches.Count > 0)
      {
        // Create duplicate record
        _logger.LogInformation("Creating duplicate record with {MatchCount} matches for person name: {PersonName}",
            duplicateMatches.Count, file.FaceId);

        var duplicateRecord = await _duplicateRecordService.CreateDuplicateRecordAsync(
            process.Id,
            file.Id,
            file.FileName,
            duplicateMatches
        );

        // Update file status to indicate it has duplicates
        using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
        {
          var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
          if (dbFile != null)
          {
            dbFile.Status = "Duplicate";
            dbFile.ProcessStatus = "Completed: Found duplicates";
            await fileSession.SaveChangesAsync();
          }
        }

        // Update in-memory file
        file.Status = "Duplicate";

        _logger.LogInformation("Created duplicate record for file: {FileName} with {MatchCount} matches",
            file.FileName, duplicateMatches.Count);
      }
      else
      {
        _logger.LogInformation("No duplicate matches found for file: {FileName}", file.FileName);
      }

      // Add to processed files
      if (!step.ProcessedFiles.Contains(file.Id))
      {
        step.ProcessedFiles.Add(file.Id);
      }
    }

    // Helper method to register a face and then identify it
    private async Task RegisterAndIdentifyFaceAsync(FileModel file, DeduplicationProcess process, ProcessStep step, string? personName = null)
    {
      // Use the provided person name or generate a new one if not provided
      if (string.IsNullOrEmpty(personName))
      {
        personName = $"person_{ComputeHash(file.Base64String)[..10]}";
      }
      _logger.LogInformation("Registering face with name: {PersonName}", personName);

      // Register the face
      var registerResult = await _t4FaceService.RegisterFaceAsync(personName, file.Base64String);

      if (registerResult.Success)
      {
        _logger.LogInformation("Successfully registered face with name: {PersonName}", personName);

        // Update file status in database
        using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
        {
          var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
          if (dbFile != null)
          {
            dbFile.Status = "Inserted";
            dbFile.ProcessStatus = "Processing";
            dbFile.FaceId = personName;
            await fileSession.SaveChangesAsync();
          }
        }

        // Update in-memory file
        file.Status = "Inserted";
        file.FaceId = personName;

        // Now identify to find potential matches
        _logger.LogInformation("Identifying newly registered face against existing faces");

        var identifyResult = await _t4FaceService.IdentifyFaceAsync(file.Base64String);

        if (identifyResult.Success && identifyResult.HasMatches)
        {
          // Filter matches above threshold and exclude the newly registered face
          var significantMatches = identifyResult.Matches
              .Where(m => m.Confidence > 70 && m.Name != personName)
              .OrderByDescending(m => m.Confidence)
              .ToList();

          if (significantMatches.Count > 0)
          {
            _logger.LogInformation("Found {MatchCount} matches above threshold for newly registered face",
                significantMatches.Count);

            // Process matches to create duplicate records
            await ProcessMatchesAsync(file, process, step, significantMatches);
          }
          else
          {
            _logger.LogInformation("No significant matches found for newly registered face");
          }
        }
        else
        {
          _logger.LogInformation("No matches found for newly registered face");
        }
      }
      else
      {
        // Handle registration failure
        _logger.LogError("Failed to register face: {Message}", registerResult.Message);

        // Update file status to error
        using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
        {
          var dbFile = await fileSession.LoadAsync<FileModel>(file.Id);
          if (dbFile != null)
          {
            dbFile.Status = "Error";
            dbFile.ProcessStatus = "Error: Face registration failed";
            await fileSession.SaveChangesAsync();
          }
        }

        // Create exception record
        await _exceptionService.CreateExceptionAsync(
            process.Id,
            file.FileName,
            new List<string> { "Error during face registration" },
            0.0,
            new Dictionary<string, object>
            {
              ["errorMessage"] = registerResult.Message,
              ["processingDate"] = DateTime.UtcNow
            }
        );
      }
    }

    // This method is kept for backward compatibility but is no longer used in the new flow
    // The functionality has been integrated into ProcessFileInsertionAsync
    private Task ProcessFileIdentificationAsync(FileModel file, DeduplicationProcess process, ProcessStep step, IAsyncDocumentSession session)
    {
      _logger.LogWarning("ProcessFileIdentificationAsync is deprecated and should not be called in the new flow");

      // Add the file to processed files to maintain compatibility
      if (!step.ProcessedFiles.Contains(file.Id))
      {
        step.ProcessedFiles.Add(file.Id);
      }

      return Task.CompletedTask;
    }

    // Helper method to get file IDs by file names
    private async Task<Dictionary<string, string>> GetFileIdsByNamesAsync(List<string> fileNames)
    {
      var result = new Dictionary<string, string>();

      try
      {
        using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);

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
        using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);

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

        // Always use the processes database for this operation

        // Use concurrency control to update the process status
        await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          RavenDbContext.DatabaseType.Processes, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          (session, loadedProcess) =>
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

            return Task.FromResult(loadedProcess);
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

        // Always use the processes database for this operation

        // Use concurrency control to update the process status
        bool canContinueProcessing = false;

        await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
          RavenDbContext.DatabaseType.Processes, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          (session, loadedProcess) =>
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

            return Task.FromResult(loadedProcess);
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

    public async Task CleanupProcessAsync(string processId, string? username = null)
    {
      // Get the process using our improved GetProcessAsync method
      var process = await GetProcessAsync(processId);
      _logger.LogInformation("Process found for cleanup: {ProcessId}", process.Id);

      // Always use the processes database for this operation

      // Use the provided username or fall back to the process username or system
      var cleanupUsername = username ?? process.Username ?? process.CreatedBy ?? "system";

      // Use concurrency control to update the process status
      process = await _context.ExecuteWithConcurrencyControlAsync<DeduplicationProcess>(
        RavenDbContext.DatabaseType.Processes, // Use the processes database
        process.Id, // Use the full document ID including the prefix
        (session, loadedProcess) =>
        {
          // Update process status to "Cleaning"
          loadedProcess.Status = "Cleaning";
          loadedProcess.CleanupDate = DateTime.UtcNow;
          loadedProcess.CleanupUsername = cleanupUsername;
          loadedProcess.CurrentStage = "Cleaning";

          _logger.LogInformation("Process {ProcessId} status updated to Cleaning by {Username}", loadedProcess.Id, cleanupUsername);
          return Task.FromResult(loadedProcess);
        },
        5 // Maximum number of retries
      );

      // Open a session for the rest of the processing
      using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Processes);

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
            using (var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files))
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
          RavenDbContext.DatabaseType.Processes, // Use the processes database
          process.Id, // Use the full document ID including the prefix
          (finalSession, loadedProcess) =>
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
            return Task.FromResult(loadedProcess);
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
            RavenDbContext.DatabaseType.Processes, // Use the processes database
            process.Id, // Use the full document ID including the prefix
            (errorSession, loadedProcess) =>
            {
              loadedProcess.Status = "Error";
              loadedProcess.CompletionNotes = $"Error during cleanup: {ex.Message}";

              _logger.LogInformation("Process {ProcessId} status updated to Error during cleanup", loadedProcess.Id);
              return Task.FromResult(loadedProcess);
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

    // Helper method to compute a hash for a string (identical to the one in T4FaceService)
    private static string ComputeHash(string input)
    {
      if (string.IsNullOrEmpty(input))
      {
        return string.Empty;
      }

      using var sha = System.Security.Cryptography.SHA256.Create();
      var bytes = System.Text.Encoding.UTF8.GetBytes(input);
      var hash = sha.ComputeHash(bytes);
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
        using var fileSession = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Files);
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
