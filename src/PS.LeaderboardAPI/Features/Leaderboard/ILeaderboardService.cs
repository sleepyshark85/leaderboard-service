using Microsoft.Extensions.Options;
using PS.LeaderboardAPI.Data;

namespace PS.LeaderboardAPI.Features.Leaderboard;

public interface ILeaderboardService
{
    /// <summary>
    /// Adds a new player to the leaderboard system
    /// </summary>
    /// <param name="name">Name of the player</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with player data</returns>
    Task<AddPlayerResult> AddPlayerAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a score for a player and updates leaderboard rankings
    /// </summary>
    /// <param name="playerId">Unique identifier for the player</param>
    /// <param name="score">Score to submit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with updated player rank, score, and leaderboard data</returns>
    Task<LeaderboardResult> SubmitScoreAsync(Guid playerId, long score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets leaderboard data for a specific player
    /// </summary>
    /// <param name="playerId">Unique identifier for the player</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with player rank, score, top players, and nearby players</returns>
    Task<LeaderboardResult> GetLeaderboardAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all player scores to zero and clears leaderboard cache
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating the number of players affected</returns>
    Task<ResetScoresResult> ResetAllScoresAsync(CancellationToken cancellationToken = default);
}

public class LeaderboardService : ILeaderboardService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IRedisLeaderboardService _redisLeaderboardService;
    private readonly ILogger<LeaderboardService> _logger;
    private readonly LeaderboardConfiguration _configuration;

    public LeaderboardService(
        IPlayerRepository playerRepository,
        IRedisLeaderboardService redisLeaderboardService,
        ILogger<LeaderboardService> logger,
        IOptions<LeaderboardConfiguration> configuration)
    {
        _playerRepository = playerRepository ?? throw new ArgumentNullException(nameof(playerRepository));
        _redisLeaderboardService = redisLeaderboardService ?? throw new ArgumentNullException(nameof(redisLeaderboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration.Value ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async Task<AddPlayerResult> AddPlayerAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding new player '{name}'", name);
            
            var newPlayer = new Player(name);
            var addedPlayer = await _playerRepository.AddAsync(newPlayer, cancellationToken);

            // Try to add to Redis leaderboard (non-critical)
            try
            {
                await _redisLeaderboardService.UpdatePlayerScoreAsync(addedPlayer.Id, 0, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add player {name} to Redis leaderboard, continuing without cache", name);
            }

            _logger.LogInformation("Successfully added new player {name}", name);
            return AddPlayerResult.CreateSuccess(MapToPlayerData(addedPlayer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add player '{name}'", name);
            return AddPlayerResult.CreateFailure($"Failed to add player: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<LeaderboardResult> SubmitScoreAsync(Guid playerId, long score, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Submitting score {Score} for player {PlayerId}", score, playerId);

            if (playerId == Guid.Empty)
            {
                return LeaderboardResult.CreateFailure("Player ID cannot be empty");
            }

            if (score < 0)
            {
                return LeaderboardResult.CreateFailure("Score cannot be negative");
            }

            //Save to PostgreSQL first
            var player = await _playerRepository.GetWithScoresAsync(playerId, cancellationToken);
            if (player is null)
            {
                return LeaderboardResult.CreateFailure("Player not found");
            }

            player.SubmitScore(score);
            await _playerRepository.UpdateAsync(player);
            
            _logger.LogInformation("Score saved to database for player {PlayerId}: {Score}", playerId, score);

            return await GetLeaderBoardInternalAsync(player);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit score {Score} for player {PlayerId}", score, playerId);
            return LeaderboardResult.CreateFailure($"Failed to submit score: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<LeaderboardResult> GetLeaderboardAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting leaderboard for player {PlayerId}", playerId);

            if (playerId == Guid.Empty)
            {
                return LeaderboardResult.CreateFailure("Player ID cannot be empty");
            }

            // Always get player from database first
            var player = await _playerRepository.GetAsync(playerId, cancellationToken);
            if (player == null)
            {
                _logger.LogWarning("Player {PlayerId} not found", playerId);
                return LeaderboardResult.CreateFailure("Player not found");
            }

            return await GetLeaderBoardInternalAsync(player);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get leaderboard for player {PlayerId}", playerId);
            return LeaderboardResult.CreateFailure($"Failed to get leaderboard: {ex.Message}");
        }
    }

    private async Task<LeaderboardResult> GetLeaderBoardInternalAsync(Player player, CancellationToken cancellationToken = default)
    { 
        // Try to get leaderboard data from Redis
            var redisAvailable = await _redisLeaderboardService.IsAvailableAsync(cancellationToken);
            
            if (redisAvailable)
            {
                var playerRank = await _redisLeaderboardService.GetPlayerRankAsync(player.Id, cancellationToken);
                var topPlayers = await _redisLeaderboardService.GetTopPlayersAsync(_configuration.TopLimit, cancellationToken);
                var nearbyPlayers = await _redisLeaderboardService.GetNearbyPlayersAsync(player.Id, _configuration.NearbyRange, cancellationToken);

                _logger.LogInformation("Successfully retrieved leaderboard from Redis for player {PlayerId}, rank: {Rank}", player.Id, playerRank);

                return LeaderboardResult.CreateSuccess(
                    playerRank,
                    player.CurrentScore,
                    topPlayers,
                    nearbyPlayers,
                    degraded: false);
            }
            else
            {
                // Redis degraded mode - return player data only
                _logger.LogWarning("Redis unavailable, returning degraded leaderboard for player {PlayerId}", player.Id);
                
                return LeaderboardResult.CreateSuccess(
                    playerRank: 0,
                    player.CurrentScore,
                    Array.Empty<LeaderboardEntry>(),
                    Array.Empty<LeaderboardEntry>(),
                    degraded: true);
            }
    }

    /// <inheritdoc />
    public async Task<ResetScoresResult> ResetAllScoresAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resetting all player scores");

            // Reset scores in database first
            var playersAffected = await _playerRepository.ResetAllScoresAsync(cancellationToken);

            _logger.LogInformation("Reset scores for {Count} players in database", playersAffected);

            // Clear Redis leaderboard
            var redisClearSuccess = await _redisLeaderboardService.ClearLeaderboardAsync(cancellationToken);

            if (redisClearSuccess)
            {
                _logger.LogInformation("Successfully cleared Redis leaderboard");
            }
            else
            {
                _logger.LogWarning("Failed to clear Redis leaderboard, but database reset was successful");
            }

            return ResetScoresResult.CreateSuccess(playersAffected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset all player scores");
            return ResetScoresResult.CreateFailure($"Failed to reset scores: {ex.Message}");
        }
    }

    private static PlayerData MapToPlayerData(Player player)
    {
        return new PlayerData
        {
            Id = player.Id,
            CurrentScore = player.CurrentScore,
            CreatedAt = player.CreatedAt,
            LastUpdatedAt = player.LastUpdatedAt
        };
    }
}

public record AddPlayerResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public PlayerData? Player { get; init; }

    public static AddPlayerResult CreateSuccess(PlayerData player) => new()
    {
        Success = true,
        Player = player
    };

    public static AddPlayerResult CreateFailure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

public record LeaderboardResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int PlayerRank { get; init; }
    public long PlayerScore { get; init; }
    public IReadOnlyList<LeaderboardEntry> TopPlayers { get; init; } = Array.Empty<LeaderboardEntry>();
    public IReadOnlyList<LeaderboardEntry> NearbyPlayers { get; init; } = Array.Empty<LeaderboardEntry>();
    public bool Degraded { get; init; }

    public static LeaderboardResult CreateSuccess(
        int playerRank,
        long playerScore,
        IReadOnlyList<LeaderboardEntry> topPlayers,
        IReadOnlyList<LeaderboardEntry> nearbyPlayers,
        bool degraded = false) => new()
    {
        Success = true,
        PlayerRank = playerRank,
        PlayerScore = playerScore,
        TopPlayers = topPlayers,
        NearbyPlayers = nearbyPlayers,
        Degraded = degraded
    };

    public static LeaderboardResult CreateFailure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

public record ResetScoresResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int PlayersAffected { get; init; }

    public static ResetScoresResult CreateSuccess(int playersAffected) => new()
    {
        Success = true,
        PlayersAffected = playersAffected
    };

    public static ResetScoresResult CreateFailure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

public record PlayerData
{
    public Guid Id { get; init; }
    public long CurrentScore { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
}

public record LeaderboardEntry
{
    public Guid PlayerId { get; init; }
    public long Score { get; init; }
    public int Rank { get; init; }
}