using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using User.DTOs;
using User.Services;
using System.Security.Claims;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiert une authentification pour tous les endpoints
    public class ProfileController : ControllerBase
    {
        private readonly UserService _userService;

        public ProfileController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        public IActionResult GetMyProfile()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var profile = _userService.GetUserProfile(userId);
            if (profile == null)
            {
                return NotFound();
            }

            return Ok(profile);
        }

        [HttpPut("update")]
        public IActionResult UpdateProfile([FromBody] UpdateProfileDTO updateProfile)
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var updatedProfile = _userService.UpdateUserProfile(userId, updateProfile);
            if (updatedProfile == null)
            {
                return NotFound();
            }

            return Ok(updatedProfile);
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

            return Ok(profile);
        }
    }
} 