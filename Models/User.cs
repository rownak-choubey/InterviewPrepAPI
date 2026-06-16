namespace InterviewPrepAPI.Models;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public string? AuthProvider { get; set; }

    public string? ExternalUserId { get; set; }

    public bool EmailConfirmed { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
}
