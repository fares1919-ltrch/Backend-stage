using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using User.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace users.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet("all")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult GetAllUsers()
        {
            var users = _userService.GetAllUsersInfo();
            return Ok(users);
        }

        [HttpPut("confirm/{userId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult ConfirmUser(string userId)
        {
            var success = _userService.ConfirmUser(userId);
            if (!success)
                return NotFound("User not found");

            return Ok(new { message = "User confirmed" });
        }

        [HttpDelete("delete/{userId}")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult DeleteUser(string userId)
        {
            var success = _userService.DeleteUser(userId);
            if (!success)
                return NotFound("User not found");

            return Ok(new { message = "User deleted" });
        }

        [HttpPut("promote/{userId}")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult PromoteToAdmin(string userId)
        {
            // Get requesting user ID if it exists in the token
            var requestingUserId = User.FindFirst("UserId")?.Value;

            // The [Authorize(Roles = "SuperAdmin")] attribute already ensures this is a SuperAdmin
            // so we can proceed even if the UserId claim is missing

            // We'll pass the requestingUserId to the service (it can be null)
            var success = _userService.PromoteToAdmin(userId, requestingUserId);
            if (!success)
                return BadRequest("Cannot promote user to admin");

            return Ok(new { message = "User promoted to admin" });
        }

        [HttpPut("demote/{userId}")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult DemoteAdmin(string userId)
        {
            // Get requesting user ID if it exists in the token
            var requestingUserId = User.FindFirst("UserId")?.Value;

            // The [Authorize(Roles = "SuperAdmin")] attribute already ensures this is a SuperAdmin
            // so we can proceed even if the UserId claim is missing

            // We'll pass the requestingUserId to the service (it can be null)
            var success = _userService.DemoteAdmin(userId, requestingUserId);
            if (!success)
                return BadRequest("Cannot demote admin");

            return Ok(new { message = "Admin demoted to user" });
        }
    }
}
