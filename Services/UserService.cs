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
using Raven.Data;

namespace User.Services
{
    public class UserService : IUserInterface
    {
        private readonly RavenDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly JwtTokenService _jwtTokenService; // Ajout de JwtTokenService

        public UserService(IConfiguration config , RavenDbContext dbContext, JwtTokenService jwtTokenService)
        {
            _dbContext = dbContext;
            _config = config;
            _jwtTokenService = jwtTokenService; // Initialisation dans le constructeur
        }

        public Task<UserModel?> RegisterUser(RegisterUserDTO registerUserDto)
        {
            // Vérification si l'email existe déjà
            using (var session = _dbContext.OpenSession(database:"users"))
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

        public string? Login(Logindata logindata)
        {
            using (var session = _dbContext.OpenSession(database:"users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.email == logindata.Email);

                if (user == null)
                {
                    Console.WriteLine("Utilisateur non trouvé avec l'email : " + logindata.Email);
                    return null;
                }

                bool isValidPassword = BCrypt.Net.BCrypt.Verify(logindata.Password, user.password);

                if (!isValidPassword)
                {
                    Console.WriteLine("Mot de passe incorrect.");
                    return null;
                }

                // Générer le token avec le rôle
                var claims = new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.email),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role.ToString()),
                    new System.Security.Claims.Claim("UserId", user.id)
                };

                return _jwtTokenService.GenerateJwtTokenWithClaims(user.email, claims);
            }
        }

        public async Task<string> GoogleLoginAsync(string idToken)
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, 
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { "110137029112-8o191dgnivc0f3al16oo2jr90ptf3er2.apps.googleusercontent.com" }
                });

            using var session = _dbContext.OpenSession(database:"users");

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
            }

            // Générer le token JWT
            return _jwtTokenService.GenerateJwtToken(payload.Email);
        }

        public Task<string?> GenerateResetToken(string resetEmail)
        {
            using (var session = _dbContext.OpenSession(database:"users"))
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
            using (var session = _dbContext.OpenSession(database:"users"))
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
            using (var session = _dbContext.OpenSession(database:"users"))
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
                var requestingUser = session.Query<UserModel>().FirstOrDefault(u => u.id == requestingUserId);
                var targetUser = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);

                if (requestingUser?.Role != UserRole.SuperAdmin || targetUser == null)
                {
                    return false;
                }

                if (targetUser.Role == UserRole.Admin)
                {
                    return false; // Already an admin
                }

                targetUser.Role = UserRole.Admin;
                session.SaveChanges();
                return true;
            }
        }

        public bool DemoteAdmin(string userId, string requestingUserId)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var requestingUser = session.Query<UserModel>().FirstOrDefault(u => u.id == requestingUserId);
                var targetUser = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);

                if (requestingUser?.Role != UserRole.SuperAdmin || targetUser == null)
                {
                    return false;
                }

                if (targetUser.Role != UserRole.Admin)
                {
                    return false; // Not an admin
                }

                targetUser.Role = UserRole.User;
                session.SaveChanges();
                return true;
            }
        }

        public bool ConfirmUser(string userId)
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

        public bool DeleteUser(string userId)
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

        public UserDTO? UpdateUserProfile(string userId, UpdateProfileDTO updateProfile)
        {
            using (var session = _dbContext.OpenSession(database: "users"))
            {
                var user = session.Query<UserModel>().FirstOrDefault(u => u.id == userId);
                if (user == null) return null;

                if (!string.IsNullOrEmpty(updateProfile.UserName))
                    user.userName = updateProfile.UserName;
                
                if (!string.IsNullOrEmpty(updateProfile.Email))
                    user.email = updateProfile.Email;

                session.SaveChanges();

                return new UserDTO
                {
                    UserId = user.id,
                    UserName = user.userName,
                    Email = user.email,
                    IsValidated = user.validated,
                    Role = user.Role
                };
            }
        }

        public UserDTO? GetUserProfile(string userId)
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
                    Role = user.Role
                };
            }
        }

        Task<UserModel> IUserInterface.RegisterUser(RegisterUserDTO registerUserDto)
        {
            throw new NotImplementedException();
        }
    }
}
