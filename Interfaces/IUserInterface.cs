using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using User.Models;
using User.DTOs;
using User.Services;

namespace User.Interfaces
{
    public interface IUserInterface
    {
        Task<UserModel?> RegisterUser(RegisterUserDTO registerUserDto);  // Register user method
        string? GenerateJwtToken(string email);  // JWT Token generation method
        LoginResult? Login(Logindata logindata);  // User login method
        Task<LoginResult> GoogleLoginAsync(string idToken);  // Google login method
        bool DeleteProfilePicture(string userId);  // Delete profile picture method
    }
}