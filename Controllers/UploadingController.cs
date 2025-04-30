using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Upload.Services;
using Microsoft.Extensions.Logging;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;
using Files.Models;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using DeduplicationFile = Backend.Models.DeduplicationFile;

namespace upp.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class UploadingController : ControllerBase
  {
    private readonly UploadService _uploadService;
    private readonly RavenDbContext _dbContext;
    private readonly ILogger<UploadingController> _logger;
    private readonly ExceptionService _exceptionService;

    public UploadingController(
        UploadService uploadService,
        RavenDbContext dbContext,
        ILogger<UploadingController> logger,
        ExceptionService exceptionService)
    {
      _uploadService = uploadService;
      _dbContext = dbContext;
      _logger = logger;
      _exceptionService = exceptionService;
    }

    [HttpPost("clear-temp")]
    public IActionResult ClearTempFolder()
    {
      try
      {
        // Clear TempFiles directory
        string tempFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles");
        if (Directory.Exists(tempFilesPath))
        {
          foreach (var dir in Directory.GetDirectories(tempFilesPath))
          {
            Directory.Delete(dir, true);
          }
        }

        return Ok(new { message = "Temp folders cleared successfully." });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error clearing temp folders");
        return StatusCode(500, new { message = "Error clearing temp folders: " + ex.Message });
      }
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDeduplicationFiles(IFormFile file)
    {
      try
      {
        // Log request information
        _logger.LogInformation("Upload request received. HasFile: {HasFile}, ContentType: {ContentType}",
            file != null, Request.ContentType);

        if (file == null || file.Length == 0)
        {
          _logger.LogWarning("No file was uploaded or file is empty");
          return BadRequest(new { success = false, message = "No file uploaded or file is empty" });
        }

        _logger.LogInformation("File received: {FileName}, Size: {FileSize} bytes",
            file.FileName, file.Length);

        // Check if file name already exists in the system
        var existingFile = await CheckIfFileNameExistsAsync(file.FileName);
        if (existingFile != null)
        {
          _logger.LogWarning("File with name {FileName} already exists with ID {FileId}",
              file.FileName, existingFile.Id);

          // Create a new process for this conflict
          var conflictProcessId = Guid.NewGuid().ToString();
          var conflictUser = User.Identity?.Name ?? "anonymous";
          var conflictProcess = new DeduplicationProcess
          {
            Id = $"processes/{conflictProcessId}",
            Name = $"Conflict-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            Username = conflictUser,
            CreatedBy = conflictUser,
            Status = "Conflict Detected",
            CreatedAt = DateTime.UtcNow,
            Files = new List<DeduplicationFile>(),
            Steps = new List<ProcessStep>(),
            FileIds = new List<string>(),
            FileCount = 0,
            ProcessedFiles = 0,
            CurrentStage = "Conflict Detected",
            CompletionNotes = ""
          };

          using (var session = _dbContext.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
          {
            await session.StoreAsync(conflictProcess);
            await session.SaveChangesAsync();
          }

          // Create a conflict record
          var conflictService = new ConflictService(_dbContext);
          var conflict = await conflictService.CreateConflictAsync(
              conflictProcess.Id,
              file.FileName,
              $"File name conflict with existing file: {existingFile.Id}",
              1.0); // 100% confidence for exact filename match

          // Return a specific response for this conflict
          return Ok(new
          {
            success = true,
            warning = true,
            message = $"File with name {file.FileName} already exists in the system. A conflict has been created.",
            conflictId = conflict.Id,
            processId = conflictProcess.Id
          });
        }

        // Individual image upload has been removed - only tar.gz files are supported

        // Handle tar.gz upload as required by documentation
        if (!file.FileName.EndsWith(".tar.gz"))
        {
          return BadRequest(new { success = false, message = "Only tar.gz files are supported" });
        }

        // Create a new deduplication process
        var processId = Guid.NewGuid().ToString();
        var currentUser = User.Identity?.Name ?? "anonymous";
        var process = new DeduplicationProcess
        {
          Id = $"processes/{processId}",
          Name = $"Process-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
          Username = currentUser,
          CreatedBy = currentUser,
          Status = "Ready to Start",
          CreatedAt = DateTime.UtcNow,
          Files = new List<DeduplicationFile>(),
          Steps = new List<ProcessStep>(),
          FileIds = new List<string>(),
          FileCount = 0,
          ProcessedFiles = 0,
          CurrentStage = "Created",
          CompletionNotes = ""
        };

        // Create directory for this process
        var processDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles", processId);
        Directory.CreateDirectory(processDirectory);

        // Save the tar.gz file temporarily
        var tarGzPath = Path.Combine(processDirectory, "upload.tar.gz");
        using (var fileStream = new FileStream(tarGzPath, FileMode.Create))
        {
          await file.CopyToAsync(fileStream);
        }

        // Validate the tar.gz file before attempting to extract it
        if (!IsValidTarGzFile(tarGzPath))
        {
          _logger.LogWarning("Uploaded file {FileName} is not a valid tar.gz archive", file.FileName);

          // Create an exception record for this error
          try
          {
            // Update process status to indicate error
            process.Status = "Error";

            // Save the process even though validation failed
            using (var session = _dbContext.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
            {
              await session.StoreAsync(process);
              await session.SaveChangesAsync();
            }

            // Create an exception record
            var exception = await _exceptionService.CreateExceptionAsync(
                process.Id,
                file.FileName,
                new List<string> { "Invalid Archive Format" },
                0.0,
                new Dictionary<string, object>
                {
                  ["errorType"] = "ValidationError",
                  ["errorMessage"] = "The uploaded file is not a valid tar.gz archive",
                  ["fileName"] = file.FileName,
                  ["fileSize"] = file.Length,
                  ["errorDetails"] = "File header validation failed - not a valid gzip format",
                  ["timestamp"] = DateTime.UtcNow
                });

            // Clean up temporary files
            try
            {
              if (System.IO.File.Exists(tarGzPath))
              {
                System.IO.File.Delete(tarGzPath);
              }

              // Try to delete the process directory if it exists and is empty
              if (Directory.Exists(processDirectory) && !Directory.EnumerateFileSystemEntries(processDirectory).Any())
              {
                Directory.Delete(processDirectory);
              }
            }
            catch (Exception cleanupEx)
            {
              _logger.LogWarning(cleanupEx, "Error cleaning up temporary files for invalid archive");
              // Continue even if cleanup fails
            }

            return BadRequest(new
            {
              success = false,
              message = "The uploaded file is not a valid tar.gz archive. Please check the file and try again.",
              errorType = "InvalidArchiveFormat",
              processId = process.Id,
              exceptionId = exception.Id
            });
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error creating exception record for invalid archive format");

            // Try to clean up even if exception creation failed
            try
            {
              if (System.IO.File.Exists(tarGzPath))
              {
                System.IO.File.Delete(tarGzPath);
              }

              if (Directory.Exists(processDirectory))
              {
                Directory.Delete(processDirectory, true);
              }
            }
            catch
            {
              // Ignore cleanup errors in this case
            }

            return BadRequest(new
            {
              success = false,
              message = "The uploaded file is not a valid tar.gz archive. Please check the file and try again."
            });
          }
        }

        // Extract the tar.gz file
        int fileCount = 0;

        try
        {
          using (var fileStream = new FileStream(tarGzPath, FileMode.Open, FileAccess.Read))
          using (var gzipStream = new GZipInputStream(fileStream))
          using (var tarArchive = new TarInputStream(gzipStream))
          {
            TarEntry tarEntry;
            while ((tarEntry = tarArchive.GetNextEntry()) != null)
            {
              if (tarEntry.IsDirectory)
                continue;

              string fileName = Path.GetFileName(tarEntry.Name);
              _logger.LogInformation("Processing file from archive: {FileName}", fileName);

              // Only process image files
              if (IsImageFile(fileName))
              {
                string filePath = Path.Combine(processDirectory, fileName);

                // Create output file stream
                using (var outputStream = new FileStream(filePath, FileMode.Create))
                {
                  tarArchive.CopyEntryContents(outputStream);
                }

                // Process the extracted file
                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
                string base64String = Convert.ToBase64String(fileBytes);

                // Check if file with same name already exists in the database
                var fileNameMatch = await CheckIfFileNameExistsAsync(fileName);
                bool hasConflict = false;

                if (fileNameMatch != null)
                {
                  _logger.LogWarning("File with name {FileName} already exists with ID {FileId}",
                      fileName, fileNameMatch.Id);

                  try
                  {
                    // Create a conflict record for this specific file
                    var conflictService = new ConflictService(_dbContext);
                    var conflict = await conflictService.CreateConflictAsync(
                        process.Id,
                        fileName,
                        $"File name conflict with existing file: {fileNameMatch.Id}",
                        1.0); // 100% confidence for exact filename match

                    _logger.LogInformation("Created conflict record {ConflictId} for file {FileName}",
                        conflict.Id, fileName);

                    hasConflict = true;
                  }
                  catch (Exception ex)
                  {
                    _logger.LogError(ex, "Error creating conflict record for file {FileName}", fileName);
                    // Continue processing even if conflict creation fails
                  }
                }

                // Create file record with proper ID format
                var fileGuid = Guid.NewGuid().ToString();
                var fileId = $"files/{fileGuid}";
                var fileModel = new Backend.Models.DeduplicationFile
                {
                  Id = fileId,
                  FileName = fileName,
                  FilePath = filePath,
                  Base64String = base64String,
                  Status = hasConflict ? "Conflict" : "Uploaded",
                  CreatedAt = DateTime.UtcNow
                };

                process.Files.Add(fileModel);
                process.FileIds.Add(fileId); // Store with prefix for better compatibility
                fileCount++;

                // Update FileCount to match the actual number of files
                process.FileCount = fileCount;

                if (hasConflict)
                {
                  // If there's a conflict, update the process status
                  if (process.Status != "Conflict Detected")
                  {
                    process.Status = "Conflict Detected";
                  }
                }

                _logger.LogInformation("Added file {FileName} with ID {FileId} to process {ProcessId}, Status: {Status}",
                    fileName, fileId, processId, fileModel.Status);
              }
              else
              {
                _logger.LogInformation("Skipping non-image file: {FileName}", fileName);
              }
            }
          }
        }
        catch (ICSharpCode.SharpZipLib.GZip.GZipException gzEx)
        {
          _logger.LogError(gzEx, "Error extracting tar.gz file: Corrupted or invalid GZIP format");

          // Create an exception record for this error
          var exceptionMetadata = new Dictionary<string, object>
          {
            ["errorType"] = "GZipException",
            ["errorMessage"] = gzEx.Message,
            ["fileName"] = file.FileName,
            ["fileSize"] = file.Length,
            ["errorDetails"] = "The uploaded file appears to be corrupted or is not a valid tar.gz archive",
            ["timestamp"] = DateTime.UtcNow
          };

          try
          {
            // Update process status to indicate error
            process.Status = "Error";

            // Save the process even though extraction failed
            using (var session = _dbContext.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
            {
              await session.StoreAsync(process);
              await session.SaveChangesAsync();
            }

            // Create an exception record
            var exception = await _exceptionService.CreateExceptionAsync(
                process.Id,
                file.FileName,
                new List<string> { "Corrupted Archive" },
                0.0,
                exceptionMetadata);

            // Clean up temporary files
            try
            {
              if (System.IO.File.Exists(tarGzPath))
              {
                System.IO.File.Delete(tarGzPath);
              }

              // Try to delete the process directory if it exists and is empty
              if (Directory.Exists(processDirectory) && !Directory.EnumerateFileSystemEntries(processDirectory).Any())
              {
                Directory.Delete(processDirectory);
              }
            }
            catch (Exception cleanupEx)
            {
              _logger.LogWarning(cleanupEx, "Error cleaning up temporary files for corrupted archive");
              // Continue even if cleanup fails
            }

            return BadRequest(new
            {
              success = false,
              message = "The uploaded file is corrupted or not a valid tar.gz archive",
              errorType = "CorruptedArchive",
              processId = process.Id,
              exceptionId = exception.Id
            });
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error creating exception record for corrupted archive");

            // Try to clean up even if exception creation failed
            try
            {
              if (System.IO.File.Exists(tarGzPath))
              {
                System.IO.File.Delete(tarGzPath);
              }

              if (Directory.Exists(processDirectory))
              {
                Directory.Delete(processDirectory, true);
              }
            }
            catch
            {
              // Ignore cleanup errors in this case
            }

            return StatusCode(500, new
            {
              success = false,
              message = "The uploaded file is corrupted and could not be processed: " + gzEx.Message
            });
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error extracting tar.gz file");

          // Create an exception record for this error
          try
          {
            // Update process status to indicate error
            process.Status = "Error";

            // Save the process even though extraction failed
            using (var session = _dbContext.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
            {
              await session.StoreAsync(process);
              await session.SaveChangesAsync();
            }

            // Create an exception record
            var exception = await _exceptionService.CreateExceptionAsync(
                process.Id,
                file.FileName,
                new List<string> { "Archive Extraction Error" },
                0.0,
                new Dictionary<string, object>
                {
                  ["errorType"] = ex.GetType().Name,
                  ["errorMessage"] = ex.Message,
                  ["fileName"] = file.FileName,
                  ["fileSize"] = file.Length,
                  ["timestamp"] = DateTime.UtcNow
                });

            // Clean up temporary files
            try
            {
              if (System.IO.File.Exists(tarGzPath))
              {
                System.IO.File.Delete(tarGzPath);
              }

              // Try to delete the process directory if it exists and is empty
              if (Directory.Exists(processDirectory) && !Directory.EnumerateFileSystemEntries(processDirectory).Any())
              {
                Directory.Delete(processDirectory);
              }
            }
            catch (Exception cleanupEx)
            {
              _logger.LogWarning(cleanupEx, "Error cleaning up temporary files for extraction error");
              // Continue even if cleanup fails
            }

            return StatusCode(500, new
            {
              success = false,
              message = "Error extracting tar.gz file: " + ex.Message,
              processId = process.Id,
              exceptionId = exception.Id
            });
          }
          catch (Exception innerEx)
          {
            _logger.LogError(innerEx, "Error creating exception record for extraction error");

            // Try to clean up even if exception creation failed
            try
            {
              if (System.IO.File.Exists(tarGzPath))
              {
                System.IO.File.Delete(tarGzPath);
              }

              if (Directory.Exists(processDirectory))
              {
                Directory.Delete(processDirectory, true);
              }
            }
            catch
            {
              // Ignore cleanup errors in this case
            }

            return StatusCode(500, new
            {
              success = false,
              message = "Error extracting tar.gz file: " + ex.Message
            });
          }
        }

        if (fileCount == 0)
        {
          _logger.LogWarning("No valid image files found in the archive");
          return BadRequest(new { success = false, message = "No valid image files found in the archive" });
        }

        // Save the process and files to the database
        try
        {
          _logger.LogInformation("Saving process {ProcessId} with {FileCount} files", processId, process.Files.Count);

          using (var session = _dbContext.OpenAsyncSession(RavenDbContext.DatabaseType.Processes))
          {
            await session.StoreAsync(process);
            await session.SaveChangesAsync();

            _logger.LogInformation("Process {ProcessId} saved successfully in processes database", processId);
          }

          // Save the files to the Files database
          using (var filesSession = _dbContext.OpenAsyncSession(database: "Files"))
          {
            foreach (var fileItem in process.Files)
            {
              // Convert DeduplicationFile to FileModel for consistency
              var fileModel = new Files.Models.FileModel
              {
                Id = fileItem.Id,
                FileName = fileItem.FileName,
                FilePath = fileItem.FilePath,
                Base64String = fileItem.Base64String,
                Status = fileItem.Status,
                CreatedAt = fileItem.CreatedAt,
                FaceId = fileItem.FaceId,
                ProcessStatus = fileItem.ProcessStatus,
                ProcessStartDate = fileItem.ProcessStartDate,
                Photodeduplique = fileItem.Photodeduplique
              };

              await filesSession.StoreAsync(fileModel);
              _logger.LogInformation("Stored file {FileName} with ID {FileId}", fileModel.FileName, fileModel.Id);
            }

            await filesSession.SaveChangesAsync();
            _logger.LogInformation("All {FileCount} files saved successfully", process.Files.Count);
          }

          // Delete the original tar.gz file
          System.IO.File.Delete(tarGzPath);

          // Check if there were any conflicts
          int conflictCount = process.Files.Count(f => f.Status == "Conflict");
          bool hasConflicts = conflictCount > 0;

          // Update process status if conflicts were detected
          if (hasConflicts && process.Status != "Conflict Detected")
          {
            process.Status = "Conflict Detected";

            // Save the updated process status
            using (var session = _dbContext.OpenSession(database: "Processes"))
            {
              session.Store(process);
              session.SaveChanges();
            }
          }

          string message = hasConflicts
              ? $"Successfully uploaded and extracted {fileCount} files. {fileCount - conflictCount} files are ready to process. {conflictCount} files have conflicts."
              : $"Successfully uploaded and extracted {fileCount} files. Process is ready to start.";

          return Ok(new
          {
            success = true,
            processId = processId,
            message = message,
            warning = hasConflicts,
            fileCount = fileCount,
            conflictCount = conflictCount
          });
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error saving to database");
          return StatusCode(500, new { success = false, message = "Error saving to database: " + ex.Message });
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error during file upload");
        return StatusCode(500, new { success = false, message = "Error during file upload: " + ex.Message });
      }
    }

    private static bool IsImageFile(string fileName)
    {
      string extension = Path.GetExtension(fileName).ToLowerInvariant();
      return extension == ".jpg" || extension == ".jpeg" || extension == ".png";
    }

    /// <summary>
    /// Checks if a file is a valid tar.gz archive by examining its header
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file is a valid tar.gz archive, false otherwise</returns>
    private bool IsValidTarGzFile(string filePath)
    {
      try
      {
        // Check if the file exists
        if (!System.IO.File.Exists(filePath))
        {
          _logger.LogWarning("File {FilePath} does not exist", filePath);
          return false;
        }

        // Check file size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 18) // Minimum size for a valid gzip file
        {
          _logger.LogWarning("File {FilePath} is too small to be a valid tar.gz file", filePath);
          return false;
        }

        // Check gzip header magic bytes
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
          byte[] header = new byte[2];
          int bytesRead = fileStream.Read(header, 0, 2);

          if (bytesRead < 2 || header[0] != 0x1F || header[1] != 0x8B)
          {
            _logger.LogWarning("File {FilePath} does not have a valid gzip header", filePath);
            return false;
          }
        }

        // Try to open the file as a gzip stream
        try
        {
          using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
          using (var gzipStream = new GZipInputStream(fileStream))
          {
            byte[] buffer = new byte[4096];
            gzipStream.Read(buffer, 0, 1); // Try to read at least one byte
          }

          return true;
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Error validating tar.gz file {FilePath}", filePath);
          return false;
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error checking if file {FilePath} is a valid tar.gz archive", filePath);
        return false;
      }
    }

    /// <summary>
    /// Gets a specific file by ID
    /// </summary>
    /// <param name="fileId">The ID of the file to retrieve</param>
    /// <returns>The file details</returns>
    [HttpGet("file/{fileId}")]
    [ProducesResponseType(typeof(Files.Models.FileModel), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetFile(string fileId)
    {
      try
      {
        _logger.LogInformation("Getting file with ID: {FileId}", fileId);

        if (string.IsNullOrEmpty(fileId))
        {
          return BadRequest(new { message = "File ID is required" });
        }

        // Ensure the fileId has the correct format
        string formattedFileId = fileId;
        if (!fileId.StartsWith("files/") && !fileId.Contains("/"))
        {
          formattedFileId = $"files/{fileId}";
          _logger.LogInformation("Reformatted file ID from {OriginalId} to {FormattedId}", fileId, formattedFileId);
        }

        // Load the file from the database
        using var session = _dbContext.OpenAsyncSession(database: "Files");
        var file = await session.LoadAsync<Files.Models.FileModel>(formattedFileId);

        if (file == null)
        {
          _logger.LogWarning("File with ID {FileId} not found", formattedFileId);
          return NotFound(new { message = $"File with ID {fileId} not found" });
        }

        // Add Base64 data for frontend display if it's missing
        if (string.IsNullOrEmpty(file.Base64String) && !string.IsNullOrEmpty(file.FilePath))
        {
          try
          {
            if (System.IO.File.Exists(file.FilePath))
            {
              byte[] fileBytes = System.IO.File.ReadAllBytes(file.FilePath);
              string base64 = Convert.ToBase64String(fileBytes);
              string mimeType = GetMimeType(file.FileName);
              file.Base64String = base64; // Just the base64 string without the data URL prefix
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error loading file data for {FileId}", file.Id);
          }
        }

        return Ok(file);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting file {FileId}", fileId);
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Gets the MIME type for a file based on its extension
    /// </summary>
    /// <param name="fileName">The name of the file</param>
    /// <returns>The MIME type</returns>
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

    private Task<Files.Models.FileModel?> CheckIfFileNameExistsAsync(string fileName)
    {
      try
      {
        _logger.LogInformation("Checking if file with name {FileName} exists in the database", fileName);

        // Use a synchronous session instead of an async session
        using var session = _dbContext.OpenSession(database: "Files");

        // RavenDB doesn't support string methods like ToLower() in queries
        // Instead, we'll get all non-deleted files and filter in memory
        var allActiveFiles = session.Query<Files.Models.FileModel>()
            .Where(f => f.Status != "Deleted")
            .ToList();

        // Normalize the filename for comparison (trim)
        string normalizedFileName = fileName.Trim();

        // Find exact matches (case-insensitive)
        var exactMatches = allActiveFiles
            .Where(f => string.Equals(f.FileName, normalizedFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exactMatches.Count > 0)
        {
          _logger.LogInformation("Found exact match for file {FileName}: {FileId}",
              fileName, exactMatches.First().Id);
          return Task.FromResult<Files.Models.FileModel?>(exactMatches.First());
        }

        // If no exact match, check for files with the same name but different extension
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(normalizedFileName);
        if (!string.IsNullOrEmpty(fileNameWithoutExtension))
        {
          var similarMatches = allActiveFiles
              .Where(f => f.FileName.Contains(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
              .ToList();

          if (similarMatches.Count > 0)
          {
            _logger.LogInformation("Found similar match for file {FileName}: {FileId}",
                fileName, similarMatches.First().Id);
            return Task.FromResult<Files.Models.FileModel?>(similarMatches.First());
          }
        }

        _logger.LogInformation("No existing file found with name {FileName}", fileName);
        return Task.FromResult<Files.Models.FileModel?>(null);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error checking if file {FileName} exists", fileName);
        return Task.FromResult<Files.Models.FileModel?>(null);
      }
    }
  }
}
