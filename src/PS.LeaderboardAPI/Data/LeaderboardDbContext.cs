using Microsoft.EntityFrameworkCore;
using PS.LeaderboardAPI.Data.Configuration;

namespace PS.LeaderboardAPI.Data;


public class LeaderboardDbContext : DbContext
{
    public LeaderboardDbContext(DbContextOptions<LeaderboardDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new PlayerConfiguration());
        modelBuilder.ApplyConfiguration(new ScoreSubmissionConfiguration());
    }
}