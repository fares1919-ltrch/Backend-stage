using System;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents.Session;
using BCrypt.Net;
using Backend.Services;
using Google.Apis.Auth;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using User.DTOs;
using User.Interfaces;
using User.Models;
using Backend.Data;
using Microsoft.AspNetCore.Http;
using System.IO;
using Upload.Services;

namespace User.Services
{
    public class UserService : IUserInterface
    {
        private readonly RavenDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly JwtTokenService _jwtTokenService; // Ajout de JwtTokenService
        private readonly UploadService _uploadService;

        public UserService(IConfiguration config, RavenDbContext dbContext, JwtTokenService jwtTokenService, UploadService uploadService)
        {
            _dbContext = dbContext;
            _config = config;
            _jwtTokenService = jwtTokenService; // Initialisation dans le constructeur
            _uploadService = uploadService;
        }

        public Task<UserModel?> RegisterUser(RegisterUserDTO registerUserDto)
        {
            // Vérification si l'email existe déjà
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                if (session.Query<UserModel>().Any(u => u.email == registerUserDto.Email))
                {
                    return Task.FromResult<UserModel?>(null); // L'email est déjà pris
                }

                // Vérifier si c'est le premier utilisateur (sera SuperAdmin)
                bool isFirstUser = !session.Query<UserModel>().Any();

                // Hashage du mot de passe
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(registerUserDto.Password);

                var newUser = new UserModel
                {
                    id = Guid.NewGuid().ToString(),
                    userName = registerUserDto.Username,
                    email = registerUserDto.Email,
                    password = hashedPassword,
                    validated = isFirstUser, // Premier utilisateur est automatiquement validé
                    Role = isFirstUser ? UserRole.SuperAdmin : registerUserDto.Role // Premier utilisateur est SuperAdmin
                };

                session.Store(newUser);
                session.SaveChanges();

                return Task.FromResult<UserModel?>(newUser);
            }
        }

        public LoginResult? Login(Logindata logindata)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                UserModel? user = null;

                // Try to find user by email if provided
                if (!string.IsNullOrEmpty(logindata.Email))
                {
                    user = session.Query<UserModel>().FirstOrDefault(u => u.email == logindata.Email);
                    Console.WriteLine($"Searching by email: {logindata.Email}");
                }

                // If not found by email, try username if provided
                if (user == null && !string.IsNullOrEmpty(logindata.Username))
                {
                    user = session.Query<UserModel>().FirstOrDefault(u => u.userName == logindata.Username);
                    Console.WriteLine($"Searching by username: {logindata.Username}");
                }

                if (user == null)
                {
                    Console.WriteLine("User not found with provided credentials");
                    return null;
                }

                bool isValidPassword = BCrypt.Net.BCrypt.Verify(logindata.Password, user.password);

                if (!isValidPassword)
                {
                    Console.WriteLine("Invalid password.");
                    return null;
                }

                // Generate token with role
                var claims = new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.email),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, user.userName),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.id)
                };

                var token = _jwtTokenService.GenerateJwtTokenWithClaims(user.email, claims);

                // Create user DTO
                var userDto = new UserDTO
                {
                    UserId = user.id,
                    UserName = user.userName,
                    Email = user.email,
                    IsValidated = user.validated,
                    Role = user.Role,
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address,
                    City = user.City,
                    Country = user.Country,
                    DateOfBirth = user.DateOfBirth,
                    ProfilePicture = user.ProfilePicture
                };

                return new LoginResult
                {
                    Token = token,
                    User = userDto
                };
            }
        }

        public async Task<LoginResult> GoogleLoginAsync(string idToken)
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { "110137029112-8o191dgnivc0f3al16oo2jr90ptf3er2.apps.googleusercontent.com" }
                });

            using var session = _dbContext.OpenSession(database: "users");

            var existingUser = session.Query<UserModel>().FirstOrDefault(u => u.email == payload.Email);

            if (existingUser == null)
            {
                var registerDto = new RegisterUserDTO
                {
                    Email = payload.Email,
                    Username = payload.Name,
                    Password = Guid.NewGuid().ToString(),
                    Confirmpassword = Guid.NewGuid().ToString(),
                    Role = UserRole.User
                };

                var newUser = await RegisterUser(registerDto);
                if (newUser == null)
                {
                    throw new Exception("L'utilisateur n'a pas pu être créé.");
                }

                // Get the newly created user
                existingUser = session.Query<UserModel>().FirstOrDefault(u => u.email == payload.Email);
            }

            // Generate token with claims
            var claims = new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, existingUser.email),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, existingUser.userName),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, existingUser.Role.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, existingUser.id)
            };

            var token = _jwtTokenService.GenerateJwtTokenWithClaims(existingUser.email, claims);

            // Create user DTO
            var userDto = new UserDTO
            {
                UserId = existingUser.id,
                UserName = existingUser.userName,
                Email = existingUser.email,
                IsValidated = existingUser.validated,
                Role = existingUser.Role,
                PhoneNumber = existingUser.PhoneNumber,
                Address = existingUser.Address,
                City = existingUser.City,
                Country = existingUser.Country,
                DateOfBirth = existingUser.DateOfBirth,
                ProfilePicture = existingUser.ProfilePicture
            };

            return new LoginResult
            {
                Token = token,
                User = userDto
            };
        }

        public Task<string?> GenerateResetToken(string resetEmail)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.email == resetEmail);
                if (user == null) return Task.FromResult<string?>(null);

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Key is missing!"));

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, resetEmail) }),
                    Expires = DateTime.UtcNow.AddMinutes(15),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
                };

                var token = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

                user.ResetToken = token;
                user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
                session.Store(user);
                session.SaveChanges();

                return Task.FromResult<string?>(token);
            }
        }

        public Task<bool> ResetPasswordAsync(string Token, string NewPassword)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.ResetToken == Token && u.ResetTokenExpiry > DateTime.UtcNow);
                if (user == null) return Task.FromResult(false);

                user.password = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                user.ResetToken = null;
                user.ResetTokenExpiry = null;
                session.Store(user);
                session.SaveChanges();
                return Task.FromResult(true);
            }
        }

        public string? GenerateJwtToken(string email)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.email == email);
                if (user == null) return null;

                var claims = new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.email),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role.ToString()),
                    new System.Security.Claims.Claim("UserId", user.id)
                };

                return _jwtTokenService.GenerateJwtTokenWithClaims(user.email, claims);
            }
        }

        public List<UserDTO> GetAllUsersInfo()
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var users = session.Query<UserModel>()
                    .Select(u => new UserDTO
                    {
                        UserId = u.id,
                        UserName = u.userName,
                        Email = u.email,
                        IsValidated = u.validated,
                        Role = u.Role
                    })
                    .ToList();

                return users;
            }
        }

        public bool PromoteToAdmin(string userId, string requestingUserId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                // First check if the target user exists
                var targetUser = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (targetUser == null)
                {
                    return false;
                }

                // If the target user is already an Admin, we can't promote them
                if (targetUser.Role == UserRole.Admin)
                {
                    return false; // Already an admin
                }

                // If requestingUserId is provided, verify that requestingUser is a SuperAdmin
                // This is a belt-and-suspenders approach since the controller already has [Authorize(Roles = "SuperAdmin")]
                if (!string.IsNullOrEmpty(requestingUserId))
                {
                    var requestingUser = session.Query<UserModel>().FirstOrDefault(u => u.id == requestingUserId);
                    if (requestingUser?.Role != UserRole.SuperAdmin)
                    {
                        return false;
                    }
                }

                // If we've made it here, we can perform the promotion
                targetUser.Role = UserRole.Admin;
                session.SaveChanges();
                return true;
            }
        }

        public bool DemoteAdmin(string userId, string requestingUserId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                // First check if the target user exists
                var targetUser = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (targetUser == null)
                {
                    return false;
                }

                // If the target user is not an Admin, we can't demote them
                if (targetUser.Role != UserRole.Admin)
                {
                    return false; // Not an admin
                }

                // If requestingUserId is provided, verify that requestingUser is a SuperAdmin
                // This is a belt-and-suspenders approach since the controller already has [Authorize(Roles = "SuperAdmin")]
                if (!string.IsNullOrEmpty(requestingUserId))
                {
                    var requestingUser = session.Query<UserModel>().FirstOrDefault(u => u.id == requestingUserId);
                    if (requestingUser?.Role != UserRole.SuperAdmin)
                    {
                        return false;
                    }
                }

                // If we've made it here, we can perform the demotion
                targetUser.Role = UserRole.User;
                session.SaveChanges();
                return true;
            }
        }

        public async Task<bool> ConfirmUserAsync(string userId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (user == null) return false;

                user.validated = true;
                session.SaveChanges();
                return true;
            }
        }

        // Add non-async alias for ConfirmUserAsync
        public bool ConfirmUser(string userId)
        {
            return ConfirmUserAsync(userId).GetAwaiter().GetResult();
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (user == null) return false;

                session.Delete(user);
                session.SaveChanges();
                return true;
            }
        }

        // Add non-async alias for DeleteUserAsync
        public bool DeleteUser(string userId)
        {
            return DeleteUserAsync(userId).GetAwaiter().GetResult();
        }

        public async Task<UserDTO?> UpdateUserProfileAsync(string userId, UpdateProfileDTO updateProfile)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (user == null) return null;

                if (!string.IsNullOrEmpty(updateProfile.UserName))
                    user.userName = updateProfile.UserName;

                if (!string.IsNullOrEmpty(updateProfile.Email))
                    user.email = updateProfile.Email;

                if (!string.IsNullOrEmpty(updateProfile.PhoneNumber))
                    user.PhoneNumber = updateProfile.PhoneNumber;

                if (!string.IsNullOrEmpty(updateProfile.Address))
                    user.Address = updateProfile.Address;

                if (!string.IsNullOrEmpty(updateProfile.City))
                    user.City = updateProfile.City;

                if (!string.IsNullOrEmpty(updateProfile.Country))
                    user.Country = updateProfile.Country;

                if (updateProfile.DateOfBirth.HasValue)
                    user.DateOfBirth = updateProfile.DateOfBirth;

                if (!string.IsNullOrEmpty(updateProfile.ProfilePicture))
                    user.ProfilePicture = updateProfile.ProfilePicture;

                session.SaveChanges();

                return new UserDTO
                {
                    UserId = user.id,
                    UserName = user.userName,
                    Email = user.email,
                    IsValidated = user.validated,
                    Role = user.Role,
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address,
                    City = user.City,
                    Country = user.Country,
                    DateOfBirth = user.DateOfBirth,
                    ProfilePicture = user.ProfilePicture
                };
            }
        }

        // Add non-async alias for UpdateUserProfileAsync
        public UserDTO? UpdateUserProfile(string userId, UpdateProfileDTO updateProfile)
        {
            return UpdateUserProfileAsync(userId, updateProfile).GetAwaiter().GetResult();
        }

        public async Task<UserDTO?> GetUserProfileAsync(string userId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (user == null) return null;

                return new UserDTO
                {
                    UserId = user.id,
                    UserName = user.userName,
                    Email = user.email,
                    IsValidated = user.validated,
                    Role = user.Role,
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address,
                    City = user.City,
                    Country = user.Country,
                    DateOfBirth = user.DateOfBirth,
                    ProfilePicture = user.ProfilePicture
                };
            }
        }

        // Add non-async alias for GetUserProfileAsync
        public UserDTO? GetUserProfile(string userId)
        {
            return GetUserProfileAsync(userId).GetAwaiter().GetResult();
        }

        public async Task<string> UploadProfilePictureAsync(string userId, IFormFile file)
        {
            string base64Image = await _uploadService.ProcessImageAsync(file);

            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Load<UserModel>(userId);
                if (user == null)
                {
                    throw new Exception("User not found");
                }

                user.ProfilePicture = base64Image;
                session.SaveChanges();

                return base64Image;
            }
        }

        public async Task<bool> DeleteProfilePictureAsync(string userId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Load<UserModel>(userId);
                if (user == null)
                {
                    return false;
                }

                user.ProfilePicture = null;
                session.SaveChanges();
                return true;
            }
        }

        public bool DeleteProfilePicture(string userId)
        {
            // Call the async method and wait for its completion
            return DeleteProfilePictureAsync(userId).GetAwaiter().GetResult();
        }

        Task<UserModel> IUserInterface.RegisterUser(RegisterUserDTO registerUserDto)
        {
            throw new NotImplementedException();
        }
    }
}
