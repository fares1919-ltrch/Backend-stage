using System;
using System.Collections.Generic;
using System.IO;
using Files.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using System.Buffers.Text;
using Microsoft.VisualBasic;
using Backend.Data;

namespace Upload.Services

{
    public class UploadService
    {

        private readonly RavenDbContext _dbContext;
        public UploadService(RavenDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        public async Task<string> ProcessImageAsync(IFormFile image)
        {
            Console.WriteLine($"Image is {image.FileName}");

            if (image == null || image.Length == 0)
                throw new ArgumentException("No file uploaded.");

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(image.FileName);
            string fileExtension = Path.GetExtension(image.FileName);

            Console.WriteLine(fileNameWithoutExt);

            // Create a unique process-specific directory using a GUID
            string processId = Guid.NewGuid().ToString();
            string tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles", processId);

            try
            {
                if (!Directory.Exists(tempDirectory))
                {
                    Directory.CreateDirectory(tempDirectory);
                }

                // Use a unique filename to avoid conflicts
                string uniqueFileName = $"{fileNameWithoutExt}_{DateTime.UtcNow.Ticks}{fileExtension}";
                string tempFilePath = Path.Combine(tempDirectory, uniqueFileName);

                using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                }

                Console.WriteLine($"Image saved in temp directory at: {tempFilePath}");

                using (var memoryStream = new MemoryStream())
                {
                    await image.CopyToAsync(memoryStream);
                    byte[] fileBytes = memoryStream.ToArray();
                    string base64String = Convert.ToBase64String(fileBytes);

                    using (var session = _dbContext.OpenAsyncSession(database: "Files"))
                    {
                        var file = new FileModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            Base64String = base64String,
                            Photodeduplique = false,
                            Status = "Inserted",
                            FileName = fileNameWithoutExt,
                            FilePath = tempFilePath,
                            CreatedAt = DateTime.UtcNow,
                            ProcessStartDate = DateTime.UtcNow,
                            ProcessStatus = "Processing"
                        };

                        await session.StoreAsync(file);
                        await session.SaveChangesAsync();

                        Console.WriteLine($"File saved with ID: {file.Id}");
                    }

                    // Clean up the temporary file after processing
                    try
                    {
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                            Console.WriteLine($"Temporary file deleted: {tempFilePath}");
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Warning: Could not delete temporary file: {ex.Message}");
                        // Non-critical error, continue processing
                    }

                    return base64String;
                }
            }
            catch (Exception ex)
            {
                // Create an exception record for this error
                Console.WriteLine($"Error processing image: {ex.Message}");

                // Clean up the directory if possible
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                        Console.WriteLine($"Cleaned up temporary directory: {tempDirectory}");
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                    Console.WriteLine($"Warning: Could not clean up temporary directory: {tempDirectory}");
                }

                throw; // Re-throw the original exception
            }
        }
    }
}

