using InterviewPrepAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrepAPI.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Text)
            .IsRequired();

        builder.Property(q => q.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(q => new { q.CategoryId, q.Difficulty });

        builder.HasOne(q => q.Category)
            .WithMany(c => c.Questions)
            .HasForeignKey(q => q.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
