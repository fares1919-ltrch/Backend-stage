using Backend.Data;
using User.DTOs;
using Backend.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Email.Services;
using User.Services;
using User.Models;


namespace Backend.Controllers
{

    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserService _authService;
        private readonly RavenDbContext _dbContext;
        private readonly EmailService _emailService;



        public AuthController(UserService authService, RavenDbContext dbContext, EmailService emailService)
        {
            _authService = authService;
            _dbContext = dbContext;
            _emailService = emailService;


        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserDTO registerUserDto)
        {
            try
            {
                var newUser = await _authService.RegisterUser(registerUserDto);
                if (newUser == null)
                {
                    return BadRequest(new { message = "Email already exists" });
                }

                // Check if this is the first user (SuperAdmin)
                bool isFirstUser = newUser.Role == UserRole.SuperAdmin;

                // Return appropriate response
                if (isFirstUser)
                {
                    return CreatedAtAction(
                        nameof(RegisterUser),
                        new { id = newUser.id },
                        new
                        {
                            user = newUser,
                            message = "Your account has been created as a Super Administrator with full system access. You can log in immediately.",
                            isFirstUser = true,
                            isValidated = true
                        }
                    );
                }
                else
                {
                    return CreatedAtAction(
                        nameof(RegisterUser),
                        new { id = newUser.id },
                        new
                        {
                            user = newUser,
                            message = "Your account has been created. Please wait for an administrator to validate your account.",
                            isFirstUser = false,
                            isValidated = newUser.validated
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] Logindata request)
        {
            Console.WriteLine($"Login attempt - Email: {request.Email}, Username: {request.Username}");

            if (string.IsNullOrEmpty(request.Email) && string.IsNullOrEmpty(request.Username))
            {
                return BadRequest(new { message = "Either email or username is required" });
            }

            var loginResult = _authService.Login(request);
            if (loginResult == null)
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            // Set cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true, // Protège contre JavaScript
                Secure = true, // HTTPS uniquement
                SameSite = SameSiteMode.None, // Permet le partage entre onglets
                Expires = DateTime.UtcNow.AddHours(2) // Expiration du cookie
            };

            Response.Cookies.Append("AuthToken", loginResult.Token, cookieOptions);

            // Return token and user info in response body
            return Ok(new
            {
                token = loginResult.Token,
                user = loginResult.User,
                redirectUrl = "/dashboard"
            });
        }


        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var loginResult = await _authService.GoogleLoginAsync(request.IdToken);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTime.UtcNow.AddHours(2)
                };

                Response.Cookies.Append("AuthToken", loginResult.Token, cookieOptions);
                return Ok(new
                {
                    token = loginResult.Token,
                    user = loginResult.User,
                    redirectUrl = "/dashboard"
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }


        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            return Ok(new { message = "Logged out successfully" });
        }



        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            var token = await _authService.GenerateResetToken(forgotPasswordDto.Email);
            if (token == null) return NotFound(new { message = "Email not found" });

            Console.WriteLine($"Reset link: http://localhost:4200/reset-password?token={token}");

            // Construire le lien de réinitialisation
            var resetUrl = $"https://mon-site.com/reset-password?token={token}";

            // // Envoyer l'email avec le lien
            // await _emailService.SendEmailAsync(forgotPasswordDto.Email, "Réinitialisation du mot de passe",
            //     $"Cliquez sur le lien suivant pour réinitialiser votre mot de passe : <a href='{resetUrl}'>Réinitialiser</a>");


            return Ok(new { message = "Reset password link sent" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            var result = await _authService.ResetPasswordAsync(request.Token, request.NewPass);
            if (!result) return BadRequest(new { message = "Invalid or expired token" });

            return Ok(new { message = "Password reset successful" });
        }
    }
    public class GoogleLoginRequest
    {
        public required string IdToken { get; set; }
    }




}
