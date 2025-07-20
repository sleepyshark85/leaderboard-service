using Microsoft.Extensions.Options;

namespace PS.LeaderboardAPI.Features.Leaderboard;

public class LeaderboardResetBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeaderboardResetBackgroundService> _logger;
    private readonly LeaderboardConfiguration _configuration;

    public LeaderboardResetBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LeaderboardResetBackgroundService> logger,
        IOptions<LeaderboardConfiguration> configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration.Value ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting automatic leaderboard reset service. Reset interval: {Hours} hours, Reset hour: {Hour}, Time zone: {TimeZone}", 
            _configuration.ResetIntervalHours, _configuration.ResetHour, _configuration.ResetTimeZone);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var nextResetTime = _configuration.GetNextResetTime();
                var delay = nextResetTime - DateTimeOffset.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next leaderboard reset scheduled for {ResetTime} UTC (in {Delay})", 
                        nextResetTime, delay);

                    await Task.Delay(delay, stoppingToken);
                }

                await PerformScheduledResetAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Automatic leaderboard reset service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in automatic leaderboard reset service");
        }
    }

    /// <summary>
    /// Performs a scheduled leaderboard reset using the application service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task PerformScheduledResetAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Performing scheduled leaderboard reset");

            using var scope = _serviceProvider.CreateScope();
            var leaderboardService = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();

            var result = await leaderboardService.ResetAllScoresAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Scheduled leaderboard reset completed successfully. {PlayersAffected} players affected", 
                    result.PlayersAffected);
                
                // Optionally send notifications, update metrics, etc.
                await NotifyResetCompletedAsync(result.PlayersAffected, cancellationToken);
            }
            else
            {
                _logger.LogError("Scheduled leaderboard reset failed: {ErrorMessage}", result.ErrorMessage);
                
                // Optionally send alerts, retry logic, etc.
                await NotifyResetFailedAsync(result.ErrorMessage, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled leaderboard reset");
            await NotifyResetFailedAsync(ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies relevant systems that a reset was completed successfully.
    /// This can be extended to send webhooks, update metrics, etc.
    /// </summary>
    /// <param name="playersAffected">Number of players affected by the reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task NotifyResetCompletedAsync(int playersAffected, CancellationToken cancellationToken)
    {
        try
        {
            // Placeholder for notification logic
            // Could include:
            // - Sending webhooks to external systems
            // - Publishing events to message queues
            // - Updating application metrics
            // - Sending notifications to administrators
            
            _logger.LogDebug("Leaderboard reset notification sent for {PlayersAffected} players", playersAffected);
            
            // Example: Log an event that monitoring systems can pick up
            using var scope = _serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetService<ILogger<LeaderboardResetBackgroundService>>();
            
            logger?.LogInformation("LEADERBOARD_RESET_COMPLETED: {PlayersAffected} players reset at {Timestamp}", 
                playersAffected, DateTimeOffset.UtcNow);
                
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send reset completion notification");
            // Don't throw - notifications are optional
        }
    }

    /// <summary>
    /// Notifies relevant systems that a reset failed.
    /// This can be extended to send alerts, create incidents, etc.
    /// </summary>
    /// <param name="errorMessage">Error message from the failed reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task NotifyResetFailedAsync(string errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            // Placeholder for alert logic
            // Could include:
            // - Sending alerts to monitoring systems
            // - Creating incidents in incident management systems
            // - Sending notifications to on-call engineers
            // - Publishing to error tracking systems
            
            _logger.LogError("LEADERBOARD_RESET_FAILED: {ErrorMessage} at {Timestamp}", 
                errorMessage, DateTimeOffset.UtcNow);
                
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send reset failure notification");
            // Don't throw - notifications are optional
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping automatic leaderboard reset service");
        await base.StopAsync(cancellationToken);
    }
}