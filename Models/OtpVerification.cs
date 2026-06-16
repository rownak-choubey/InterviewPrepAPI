using System.ComponentModel.DataAnnotations;

namespace InterviewPrepAPI.Models;

public class OtpVerification : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string CodeHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    [MaxLength(200)]
    public string? PasswordHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public string Purpose { get; set; } = "password_reset";

    public int AttemptCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}
