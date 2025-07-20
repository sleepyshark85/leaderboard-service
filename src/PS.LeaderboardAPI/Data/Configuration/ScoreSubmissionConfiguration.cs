using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PS.LeaderboardAPI.Data.Configuration;

public class ScoreSubmissionConfiguration : IEntityTypeConfiguration<ScoreSubmission>
{
    public void Configure(EntityTypeBuilder<ScoreSubmission> builder)
    {
        builder.ToTable("score_submissions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(s => s.PlayerId)
            .HasColumnName("player_id")
            .IsRequired();

        builder.Property(s => s.Score)
            .HasColumnName("score")
            .IsRequired();

        builder.Property(s => s.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(s => s.PlayerId)
            .HasDatabaseName("ix_score_submissions_player_id")
            .HasMethod("btree");

        builder.HasIndex(s => s.Score)
            .HasDatabaseName("ix_score_submissions_score")
            .HasMethod("btree");

        builder.HasIndex(s => new { s.PlayerId, s.Score })
            .HasDatabaseName("ix_score_submissions_player_id_score")
            .HasMethod("btree");
    }
}