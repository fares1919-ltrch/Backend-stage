using User.Models;

namespace User.DTOs
{
 public class RegisterUserDTO
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string Confirmpassword { get; set; }
    public UserRole Role { get; set; } = UserRole.Admin;
}

  public class Logindata
{
    public required string Email { get; set; }
    public required string Password { get; set; }

}

 public class UserDTO
    {
    public required string UserId { get; set;}
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public bool IsValidated { get; set; }
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? ProfilePicture { get; set; }
}

 public class UpdateProfileDTO
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? ProfilePicture { get; set; }
}

public class ForgotPasswordDto
{
    public required string Email { get; set; }
}

public class ResetPasswordDto
{
    public required string Token { get; set; }
    public required string NewPass { get; set; }
}

public class GoogleLoginRequest
{
    public required string IdToken { get; set; }
}

public class AuthResponse
{
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public required DateTime Expiration { get; set; }
}

public class ErrorResponse
{
    public required string Message { get; set; }
    public required int StatusCode { get; set; }
}

public class AuthSuccessResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public AuthResponse Data { get; set; }
}

public class UploadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string FileId { get; set; }
}

}   