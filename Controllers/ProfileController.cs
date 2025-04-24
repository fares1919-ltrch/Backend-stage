using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using User.DTOs;
using User.Services;
using Backend.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiert une authentification pour tous les endpoints
    public class ProfileController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly ProfileImageService _profileImageService;

        public ProfileController(UserService userService, ProfileImageService profileImageService)
        {
            _userService = userService;
            _profileImageService = profileImageService;
        }

        [HttpGet("me")]
        public IActionResult GetMyProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var profile = _userService.GetUserProfile(userId);
            if (profile == null)
            {
                return NotFound();
            }

            // Check if user has a profile image and convert to base64 if exists
            var imagePath = _profileImageService.GetProfileImagePath(userId);
            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                string base64Image = Convert.ToBase64String(imageBytes);
                string mimeType = Path.GetExtension(imagePath).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
                profile.ProfilePicture = $"data:{mimeType};base64,{base64Image}";
            }

            return Ok(profile);
        }

        [HttpPut("update")]
        public IActionResult UpdateProfile([FromBody] UpdateProfileDTO updateProfile)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var updatedProfile = _userService.UpdateUserProfile(userId, updateProfile);
            if (updatedProfile == null)
            {
                return NotFound();
            }

            // Check if user has a profile image and convert to base64 if exists
            var imagePath = _profileImageService.GetProfileImagePath(userId);
            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                string base64Image = Convert.ToBase64String(imageBytes);
                string mimeType = Path.GetExtension(imagePath).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
                updatedProfile.ProfilePicture = $"data:{mimeType};base64,{base64Image}";
            }

            return Ok(new {
                success = true,
                message = "Profile updated successfully",
                profile = updatedProfile
            });
        }

        [HttpPost("upload-picture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            try
            {
                // Save the image file to disk
                string imagePath = await _profileImageService.SaveProfileImageAsync(userId, file);

                // Read back the image as base64
                byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                string base64Image = Convert.ToBase64String(imageBytes);
                string mimeType = Path.GetExtension(imagePath).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
                string dataUrl = $"data:{mimeType};base64,{base64Image}";

                // Update user profile with the image as base64
                var updateProfile = new UpdateProfileDTO
                {
                    ProfilePicture = dataUrl
                };

                var updatedProfile = _userService.UpdateUserProfile(userId, updateProfile);

                return Ok(new {
                    success = true,
                    message = "Profile picture uploaded successfully",
                    profilePictureUrl = dataUrl
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("picture")]
        public IActionResult DeleteProfilePicture()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Delete the image file
            bool fileDeleted = _profileImageService.DeleteProfileImageFile(userId);

            // Update user profile to remove image reference
            var updateProfile = new UpdateProfileDTO
            {
                ProfilePicture = null
            };

            var updatedProfile = _userService.UpdateUserProfile(userId, updateProfile);

            if (!fileDeleted && updatedProfile == null)
            {
                return NotFound("Profile picture not found");
            }

            return Ok(new {
                success = true,
                message = "Profile picture deleted successfully"
            });
        }

        // Kept as fallback but no longer used - we now use base64 data
        [HttpGet("images/{fileName}")]
        [AllowAnonymous] // Allow anonymous access to profile images
        public IActionResult GetProfileImage(string fileName)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "ProfileImages", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            // Determine content type based on file extension
            string contentType = "image/jpeg";
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (extension == ".png")
            {
                contentType = "image/png";
            }
            else if (extension == ".jpg" || extension == ".jpeg")
            {
                contentType = "image/jpeg";
            }

            // Return the file as a stream
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, contentType);
        }

        [HttpGet("{userId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult GetUserProfile(string userId)
        {
            var profile = _userService.GetUserProfile(userId);
            if (profile == null)
            {
                return NotFound();
            }

            // Check if user has a profile image and convert to base64 if exists
            var imagePath = _profileImageService.GetProfileImagePath(userId);
            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                string base64Image = Convert.ToBase64String(imageBytes);
                string mimeType = Path.GetExtension(imagePath).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
                profile.ProfilePicture = $"data:{mimeType};base64,{base64Image}";
            }

            return Ok(profile);
        }
    }
}
