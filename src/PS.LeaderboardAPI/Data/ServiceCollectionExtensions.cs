using Microsoft.EntityFrameworkCore;

namespace PS.LeaderboardAPI.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LeaderboardDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection string 'DefaultConnection' not found in configuration.");
            }

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Enable retry on failure for resilience
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);

                npgsqlOptions.CommandTimeout(30);
            });

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (environment == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
                options.LogTo(Console.WriteLine, LogLevel.Information);
            }
            else
            {
                options.EnableServiceProviderCaching();
                options.EnableSensitiveDataLogging(false);
            }
        });

        services.AddScoped<IPlayerRepository, PlayerRepository>();

        return services;
    }
    
    public static async Task EnsureDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LeaderboardDbContext>();
        
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetService<ILogger<LeaderboardDbContext>>();
            logger?.LogError(ex, "An error occurred while migrating the database");
            throw;
        }
    }
}