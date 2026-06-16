using System.ComponentModel.DataAnnotations;

namespace InterviewPrepAPI.Models;

public class Question : BaseEntity
{
    [Required]
    public string Text { get; set; } = string.Empty;

    public string? Answer { get; set; }

    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
}
