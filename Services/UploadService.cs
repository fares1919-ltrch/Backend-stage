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
using Raven.Data;

namespace Upload.Services

{
    public class UploadService
    {
        
        private readonly RavenDbContext _dbContext;
    public UploadService (RavenDbContext dbContext){
        _dbContext = dbContext;}


public async Task<string> ProcessImageAsync(IFormFile image)

    {
        Console.WriteLine($"Image is {image.FileName}");

    if (image == null || image.Length == 0)
        throw new ArgumentException("No file uploaded.");

    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(image.FileName);
    string fileExtension = Path.GetExtension(image.FileName);

    Console.WriteLine(fileNameWithoutExt);

    string tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TempImages");

    if (!Directory.Exists(tempDirectory))
    {
        Directory.CreateDirectory(tempDirectory);
    }

    string tempFilePath = Path.Combine(tempDirectory, $"{fileNameWithoutExt}_{Guid.NewGuid()}{fileExtension}");

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
                CreatedAt = DateTime.UtcNow,
                ProcessStartDate = DateTime.UtcNow,
                ProcessStatus = "Processing"
            };

            await session.StoreAsync(file);
            await session.SaveChangesAsync();

            Console.WriteLine($"File saved with ID: {file.Id}");
        }

        return base64String;
    }
    }
    }}

