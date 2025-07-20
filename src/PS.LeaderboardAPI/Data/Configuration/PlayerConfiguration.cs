using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PS.LeaderboardAPI.Data.Configuration;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(p => p.CurrentScore)
            .HasColumnName("current_score")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Metadata.FindNavigation(nameof(Player.ScoreSubmissions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.ScoreSubmissions)
            .WithOne()
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.CurrentScore)
            .HasMethod("btree");
    }
}