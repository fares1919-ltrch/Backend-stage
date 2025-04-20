using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using User.Models;
using User.DTOs;

namespace User.Interfaces
{
    public interface IUserInterface
    {
        Task<UserModel?> RegisterUser(RegisterUserDTO registerUserDto);  // Register user method
        string? GenerateJwtToken(string email);  // JWT Token generation method
        string? Login(Logindata logindata);  // User login method
    }
}