using Microsoft.Extensions.Options;
using PS.LeaderboardAPI.Data;
using StackExchange.Redis;

namespace PS.LeaderboardAPI.Features.Leaderboard;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeaderboardFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LeaderboardConfiguration>(configuration.GetSection(LeaderboardConfiguration.SectionName));

        services.AddRedisServices(configuration);

        services.AddScoped<ILeaderboardService, LeaderboardService>();

        services.AddHostedService<LeaderboardResetBackgroundService>();

        return services;
    }

    private static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost:6379"));
        services.AddScoped<IRedisLeaderboardService, RedisLeaderboardService>();

        return services;
    }

    public static async Task WarmupCacheAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IOptions<LeaderboardConfiguration>>().Value;
        
        var logger = scope.ServiceProvider.GetService<ILogger<LeaderboardConfiguration>>();
        
        try
        {
            logger?.LogInformation("Starting Redis cache warmup");

            var redisService = scope.ServiceProvider.GetRequiredService<IRedisLeaderboardService>();
            var playerRepository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();

            // Check if Redis is available
            if (!await redisService.IsAvailableAsync())
            {
                logger?.LogWarning("Redis is not available, skipping cache warmup");
                return;
            }
            
            // Get players from database, by batch
            var count = 1000;
            var page = 1;
            IEnumerable<Player>? retrieved;
            var cacheCount = 0;

            do
            {
                retrieved = await playerRepository.GetRangeAsync(count, page);
                if (retrieved is not null)
                {
                    var playerData = retrieved.Select(p => (p.Id, p.CurrentScore)).ToList();

                    if (await redisService.RebuildLeaderboardAsync(playerData))
                        cacheCount += playerData.Count();
                }
            }
            //Simple loop condition
            while (retrieved is not null && retrieved.Count() == count);
            
            logger?.LogInformation("Successfully warmed up Redis cache with {Count} players", cacheCount);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during Redis cache warmup");
        }
    }
}