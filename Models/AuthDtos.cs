using System.ComponentModel.DataAnnotations;

namespace InterviewPrepAPI.Models;

public class RefreshToken : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public class RefreshTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class AuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public record RegisterDto(string Username, string Email, string Password);
public record LoginDto(string Email, string Password);
public record ForgotPasswordDto(string Email);
public record VerifyOtpDto(string Email, string Code);
public record ResetPasswordDto(string Email, string Code, string NewPassword);
