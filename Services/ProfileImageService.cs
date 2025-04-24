using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Backend.Services
{
    public class ProfileImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly string _profileImagesPath;
        private readonly string _profileImagesUrlBase;

        public ProfileImageService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;

            // Create profile images directory if it doesn't exist
            _profileImagesPath = Path.Combine(_environment.ContentRootPath, "ProfileImages");
            if (!Directory.Exists(_profileImagesPath))
            {
                Directory.CreateDirectory(_profileImagesPath);
            }

            // Get the base URL for serving profile images from configuration or use default
            _profileImagesUrlBase = _configuration["ProfileImages:UrlBase"] ?? "/api/profile/images/";
        }

        public async Task<string> SaveProfileImageAsync(string userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file uploaded");
            }

            // Validate file type
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension) ||
                (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".png"))
            {
                throw new ArgumentException("Invalid file type. Only JPEG, JPG and PNG files are allowed.");
            }

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
            {
                throw new ArgumentException("File size too large. Maximum size is 5MB.");
            }

            // Delete existing profile image if it exists
            DeleteProfileImageFile(userId);

            // Create a unique filename using userId
            string fileName = $"{userId}{fileExtension}";
            string filePath = Path.Combine(_profileImagesPath, fileName);

            // Save the file to disk
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return the file path
            return filePath;
        }

        public bool DeleteProfileImageFile(string userId)
        {
            // Find all possible image files for this user
            var possibleExtensions = new[] { ".jpg", ".jpeg", ".png" };
            bool deleted = false;

            foreach (var ext in possibleExtensions)
            {
                string filePath = Path.Combine(_profileImagesPath, $"{userId}{ext}");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    deleted = true;
                }
            }

            return deleted;
        }

        public string GetProfileImageUrl(string userId)
        {
            // Check if a profile image exists for this user
            var possibleExtensions = new[] { ".jpg", ".jpeg", ".png" };

            foreach (var ext in possibleExtensions)
            {
                string filePath = Path.Combine(_profileImagesPath, $"{userId}{ext}");
                if (File.Exists(filePath))
                {
                    return $"/api/profile/images/{userId}{ext}";
                }
            }

            // Return null if no image exists
            return null;
        }

        public string GetProfileImagePath(string userId)
        {
            // Check if a profile image exists for this user
            var possibleExtensions = new[] { ".jpg", ".jpeg", ".png" };

            foreach (var ext in possibleExtensions)
            {
                string filePath = Path.Combine(_profileImagesPath, $"{userId}{ext}");
                if (File.Exists(filePath))
                {
                    return filePath;
                }
            }

            // Return null if no image exists
            return null;
        }
    }
}
