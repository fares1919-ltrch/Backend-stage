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

namespace upp.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class UploadingController : ControllerBase
  {
    private readonly UploadService _uploadService;
    private readonly RavenDbContext _dbContext;
    private readonly ILogger<UploadingController> _logger;

    public UploadingController(UploadService uploadService, RavenDbContext dbContext, ILogger<UploadingController> logger)
    {
      _uploadService = uploadService;
      _dbContext = dbContext;
      _logger = logger;
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
          var conflictProcess = new DeduplicationProcess
          {
            Id = $"processes/{conflictProcessId}",
            Name = $"Conflict-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
            Username = User.Identity?.Name ?? "anonymous",
            Status = "Conflict Detected",
            CreatedAt = DateTime.UtcNow,
            Files = new List<DeduplicationFile>(),
            Steps = new List<ProcessStep>(),
            FileIds = new List<string>()
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

        // Handle single image upload for testing purposes
        if (file != null && (file.FileName.EndsWith(".jpg") || file.FileName.EndsWith(".jpeg") || file.FileName.EndsWith(".png")))
        {
          var base64 = await _uploadService.ProcessImageAsync(file);
          return Ok(new
          {
            success = true,
            message = "Image uploaded successfully",
            base64 = base64,
          });
        }

        // Handle tar.gz upload as required by documentation
        if (!file.FileName.EndsWith(".tar.gz"))
        {
          return BadRequest(new { success = false, message = "Only tar.gz files are supported" });
        }

        // Create a new deduplication process
        var processId = Guid.NewGuid().ToString();
        var process = new DeduplicationProcess
        {
          Id = $"processes/{processId}",
          Name = $"Process-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
          Username = User.Identity?.Name ?? "anonymous",
          Status = "Ready to Start",
          CreatedAt = DateTime.UtcNow,
          Files = new List<DeduplicationFile>(),
          Steps = new List<ProcessStep>(),
          FileIds = new List<string>()
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

                // Create file record
                var fileId = Guid.NewGuid().ToString();
                var fileModel = new DeduplicationFile
                {
                  Id = fileId,
                  FileName = fileName,
                  FilePath = filePath,
                  Base64String = base64String,
                  Status = "Uploaded",
                  CreatedAt = DateTime.UtcNow
                };

                process.Files.Add(fileModel);
                process.FileIds.Add(fileId);
                fileCount++;

                _logger.LogInformation("Added file {FileName} with ID {FileId} to process {ProcessId}",
                    fileName, fileId, processId);
              }
              else
              {
                _logger.LogInformation("Skipping non-image file: {FileName}", fileName);
              }
            }
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error extracting tar.gz file");
          return StatusCode(500, new { success = false, message = "Error extracting tar.gz file: " + ex.Message });
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
              var fileModel = new FileModel
              {
                Id = fileItem.Id,
                FileName = fileItem.FileName,
                FilePath = fileItem.FilePath,
                Base64String = fileItem.Base64String,
                Status = fileItem.Status,
                CreatedAt = fileItem.CreatedAt,
                FaceId = fileItem.FaceId,
                ProcessStartDate = fileItem.ProcessStartDate,
                ProcessStatus = fileItem.ProcessStatus,
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

          return Ok(new
          {
            success = true,
            processId = processId,
            message = $"Successfully uploaded and extracted {fileCount} files. Process is ready to start."
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

    private bool IsImageFile(string fileName)
    {
      string extension = Path.GetExtension(fileName).ToLowerInvariant();
      return extension == ".jpg" || extension == ".jpeg" || extension == ".png";
    }

    private async Task<Files.Models.FileModel> CheckIfFileNameExistsAsync(string fileName)
    {
      // Use a synchronous session instead of an async session
      using var session = _dbContext.OpenSession(database: "Files");
      var query = session.Query<Files.Models.FileModel>()
          .Where(f => f.FileName == fileName && f.Status != "Deleted");

      // Use ToList() with synchronous session
      var existingFiles = query.ToList();
      return existingFiles.FirstOrDefault();
    }
  }
}
