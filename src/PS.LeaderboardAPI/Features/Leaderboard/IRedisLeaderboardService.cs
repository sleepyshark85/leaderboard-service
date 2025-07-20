using System.Text.Json;
using StackExchange.Redis;

namespace PS.LeaderboardAPI.Features.Leaderboard;

public interface IRedisLeaderboardService
{
    /// <summary>
    /// Updates a player's score in the Redis leaderboard
    /// </summary>
    /// <param name="playerId">Player's unique identifier</param>
    /// <param name="score">New score</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if Redis is unavailable</returns>
    Task<bool> UpdatePlayerScoreAsync(Guid playerId, long score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a player's current rank in the leaderboard
    /// </summary>
    /// <param name="playerId">Player's unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player rank (1-based) or 0 if not found or Redis unavailable</returns>
    Task<int> GetPlayerRankAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the top players from the leaderboard
    /// </summary>
    /// <param name="count">Number of top players to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of top players or empty list if Redis unavailable</returns>
    Task<IReadOnlyList<LeaderboardEntry>> GetTopPlayersAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets players near a specific player's rank
    /// </summary>
    /// <param name="playerId">Target player's unique identifier</param>
    /// <param name="range">Number of players above and below to include</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of nearby players or empty list if Redis unavailable</returns>
    Task<IReadOnlyList<LeaderboardEntry>> GetNearbyPlayersAsync(Guid playerId, int range, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entire leaderboard
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if Redis is unavailable</returns>
    Task<bool> ClearLeaderboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if Redis is currently available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if Redis is available, false otherwise</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds the leaderboard from database data
    /// Used for cache warming or recovery after Redis outages
    /// </summary>
    /// <param name="players">Players to add to the leaderboard</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false if Redis is unavailable</returns>
    Task<bool> RebuildLeaderboardAsync(IEnumerable<(Guid PlayerId, long Score)> players, CancellationToken cancellationToken = default);
}

public class RedisLeaderboardService : IRedisLeaderboardService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisLeaderboardService> _logger;

    private const string LeaderboardKey = "leaderboard:scores";
    private const string PlayerDataKey = "leaderboard:players";

    public RedisLeaderboardService(IConnectionMultiplexer redis, ILogger<RedisLeaderboardService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _database = _redis.GetDatabase();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePlayerScoreAsync(Guid playerId, long score, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await IsAvailableAsync(cancellationToken))
                return false;

            await UpdateInternalAsync([(playerId, score)]);

            _logger.LogDebug("Updated Redis leaderboard for player {PlayerId} with score {Score}", playerId, score);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Redis leaderboard for player {PlayerId}", playerId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetPlayerRankAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await IsAvailableAsync(cancellationToken))
                return 0;

            var playerIdString = playerId.ToString();

            // Get rank from sorted set (descending order, so higher scores = lower rank numbers)
            var rank = await _database.SortedSetRankAsync(LeaderboardKey, playerIdString, Order.Descending);

            // Convert to 1-based ranking, return 0 if not found
            return rank.HasValue ? (int)(rank.Value + 1) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Redis rank for player {PlayerId}", playerId);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopPlayersAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await IsAvailableAsync(cancellationToken) || count <= 0)
                return Array.Empty<LeaderboardEntry>();

            var result = await GetPlayerInRankRangeAsync(0, count - 1);

            _logger.LogDebug("Retrieved {Count} top players from Redis leaderboard", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get top players from Redis leaderboard");
            return Array.Empty<LeaderboardEntry>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeaderboardEntry>> GetNearbyPlayersAsync(Guid playerId, int range, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await IsAvailableAsync(cancellationToken) || range <= 0)
                return Array.Empty<LeaderboardEntry>();

            var playerIdString = playerId.ToString();

            // Get player's current rank
            var playerRank = await _database.SortedSetRankAsync(LeaderboardKey, playerIdString, Order.Descending);
            if (!playerRank.HasValue)
                return Array.Empty<LeaderboardEntry>();

            // Calculate range bounds
            var startRank = Math.Max(0, playerRank.Value - range);
            var endRank = playerRank.Value + range;

            var result = await GetPlayerInRankRangeAsync(startRank, endRank);

            _logger.LogDebug("Retrieved {Count} nearby players for player {PlayerId} from Redis leaderboard", result.Count, playerId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get nearby players for {PlayerId} from Redis leaderboard", playerId);
            return Array.Empty<LeaderboardEntry>();
        }
    }

    private async Task<IReadOnlyList<LeaderboardEntry>> GetPlayerInRankRangeAsync(long startRank, long endRank)
    {
        // Get players in the range
        var nearbyPlayers = await _database.SortedSetRangeByRankWithScoresAsync(
            LeaderboardKey,
            start: startRank,
            stop: endRank,
            order: Order.Descending);

        var result = new List<LeaderboardEntry>();

        for (int i = 0; i < nearbyPlayers.Length; i++)
        {
            if (Guid.TryParse(nearbyPlayers[i].Element, out var nearbyPlayerId))
            {
                result.Add(new LeaderboardEntry
                {
                    PlayerId = nearbyPlayerId,
                    Score = (long)nearbyPlayers[i].Score,
                    Rank = (int)(startRank + i + 1) // 1-based ranking
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> ClearLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await IsAvailableAsync(cancellationToken))
                return false;

            var batch = _database.CreateBatch();
            var clearLeaderboardTask = batch.KeyDeleteAsync(LeaderboardKey);
            var clearMetadataTask = batch.KeyDeleteAsync(PlayerDataKey);

            batch.Execute();
            await Task.WhenAll(clearLeaderboardTask, clearMetadataTask);

            _logger.LogInformation("Cleared Redis leaderboard");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Redis leaderboard");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Quick connectivity check
            if (!_redis.IsConnected)
                return false;

            // Perform a simple ping to verify Redis is responsive
            await _database.PingAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis availability check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RebuildLeaderboardAsync(IEnumerable<(Guid PlayerId, long Score)> players, CancellationToken cancellationToken = default)
    {
        try
        {
            var playerList = players.ToList();
            _logger.LogInformation("Rebuilding Redis leaderboard with {Count} players", playerList.Count);

            if (!playerList.Any())
                return true;

            await UpdateInternalAsync(playerList, cancellationToken);

            _logger.LogInformation("Successfully rebuilt Redis leaderboard with {Count} players", playerList.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild Redis leaderboard");
            return false;
        }
    }

    private async Task UpdateInternalAsync(IEnumerable<(Guid PlayerId, long Score)> players, CancellationToken cancellationToken = default)
    { 
        var batch = _database.CreateBatch();
        var tasks = new List<Task>();
        
        foreach (var player in players)
        {
            // Add to sorted set
            tasks.Add(batch.SortedSetAddAsync(LeaderboardKey, player.PlayerId.ToString(), player.Score));

            // Add metadata
            var playerData = new { player.PlayerId, player.Score, UpdatedAt = DateTime.UtcNow };
            tasks.Add(batch.HashSetAsync(PlayerDataKey, player.PlayerId.ToString(), JsonSerializer.Serialize(playerData)));
        }

        batch.Execute();
        await Task.WhenAll(tasks);
    }
}