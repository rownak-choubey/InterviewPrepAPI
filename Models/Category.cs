using System.ComponentModel.DataAnnotations;

namespace InterviewPrepAPI.Models;

public class Category : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
