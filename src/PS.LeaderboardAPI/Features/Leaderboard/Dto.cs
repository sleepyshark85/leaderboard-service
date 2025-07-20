using System.ComponentModel.DataAnnotations;

namespace PS.LeaderboardAPI.Features.Leaderboard;

/// <summary>
/// Request model for adding a new player to the leaderboard
/// </summary>
public class AddPlayerRequest
{
    /// <summary>
    /// Name of the player
    /// </summary>
    [Required(ErrorMessage = "Player name is required")]
    public string Name { get; set; }
}

/// <summary>
/// Request model for submitting a player's score
/// </summary>
public class SubmitScoreRequest
{
    /// <summary>
    /// Unique identifier for the player submitting the score
    /// </summary>
    /// <example>123e4567-e89b-12d3-a456-426614174000</example>
    [Required(ErrorMessage = "Player ID is required")]
    public Guid PlayerId { get; set; }

    /// <summary>
    /// The score to submit for the player
    /// </summary>
    /// <example>1500</example>
    [Required(ErrorMessage = "Score is required")]
    [Range(0, long.MaxValue, ErrorMessage = "Score must be non-negative")]
    public long Score { get; set; }
}

/// <summary>
/// Request model for getting leaderboard information (query parameters)
/// </summary>
public class GetLeaderboardRequest
{
    /// <summary>
    /// Unique identifier for the player to get leaderboard information for
    /// </summary>
    /// <example>123e4567-e89b-12d3-a456-426614174000</example>
    [Required(ErrorMessage = "Player ID is required")]
    public Guid PlayerId { get; set; }
}

/// <summary>
/// No request body needed for reset operation
/// </summary>
public class ResetScoresRequest
{
    // No parameters needed for reset operation
    // This class exists for consistency and potential future extension
}

/// <summary>
/// Response model for player addition operation
/// </summary>
public class AddPlayerResponse
{
    /// <summary>
    /// Unique identifier for the added player
    /// </summary>
    /// <example>123e4567-e89b-12d3-a456-426614174000</example>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Player's current score (starts at 0 for new players)
    /// </summary>
    /// <example>0</example>
    public long CurrentScore { get; set; }

    /// <summary>
    /// Timestamp when the player was created
    /// </summary>
    /// <example>2025-07-20T10:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the player was last updated
    /// </summary>
    /// <example>2025-07-20T10:30:00Z</example>
    public DateTime LastUpdatedAt { get; set; }
}

/// <summary>
/// Response model for score submission operation
/// </summary>
public class SubmitScoreResponse
{
    /// <summary>
    /// Player's current rank in the leaderboard (1-based)
    /// Returns 0 if Redis is unavailable (degraded mode)
    /// </summary>
    /// <example>1247</example>
    public int PlayerRank { get; set; }

    /// <summary>
    /// Player's updated score
    /// </summary>
    /// <example>1500</example>
    public long PlayerScore { get; set; }

    /// <summary>
    /// List of top players in the leaderboard
    /// Empty if Redis is unavailable (degraded mode)
    /// </summary>
    public List<PlayerRanking> TopPlayers { get; set; } = new();

    /// <summary>
    /// List of players near the submitting player's rank
    /// Empty if Redis is unavailable (degraded mode)
    /// </summary>
    public List<PlayerRanking> NearbyPlayers { get; set; } = new();

    /// <summary>
    /// Indicates if the response is degraded due to Redis unavailability
    /// When true, only PlayerScore is reliable; rank and player lists are empty
    /// </summary>
    /// <example>false</example>
    public bool Degraded { get; set; }
}

/// <summary>
/// Response model for getting leaderboard information
/// </summary>
public class GetLeaderboardResponse
{
    /// <summary>
    /// Player's current rank in the leaderboard (1-based)
    /// Returns 0 if Redis is unavailable (degraded mode)
    /// </summary>
    /// <example>1247</example>
    public int PlayerRank { get; set; }

    /// <summary>
    /// Player's current score
    /// </summary>
    /// <example>1500</example>
    public long PlayerScore { get; set; }

    /// <summary>
    /// List of top players in the leaderboard
    /// Empty if Redis is unavailable (degraded mode)
    /// </summary>
    public List<PlayerRanking> TopPlayers { get; set; } = new();

    /// <summary>
    /// List of players near the requested player's rank
    /// Empty if Redis is unavailable (degraded mode)
    /// </summary>
    public List<PlayerRanking> NearbyPlayers { get; set; } = new();

    /// <summary>
    /// Indicates if the response is degraded due to Redis unavailability
    /// When true, only PlayerScore is reliable; rank and player lists are empty
    /// </summary>
    /// <example>false</example>
    public bool Degraded { get; set; }
}

/// <summary>
/// Response model for score reset operation
/// </summary>
public class ResetScoresResponse
{
    /// <summary>
    /// Number of players whose scores were reset
    /// </summary>
    /// <example>1250</example>
    public int PlayersAffected { get; set; }

    /// <summary>
    /// Timestamp when the reset operation was completed
    /// </summary>
    /// <example>2025-07-20T10:30:00Z</example>
    public DateTime ResetAt { get; set; }

    /// <summary>
    /// Indicates if the reset operation was successful
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }
}

/// <summary>
/// Standard error response model
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong
    /// </summary>
    /// <example>Player not found</example>
    [Required]
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Optional error code for programmatic error handling
    /// </summary>
    /// <example>PLAYER_NOT_FOUND</example>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Additional details about the error (used in development)
    /// </summary>
    public object? Details { get; set; }
}

/// <summary>
/// Model representing a player's ranking information
/// </summary>
public class PlayerRanking
{
    /// <summary>
    /// Player's unique identifier
    /// </summary>
    /// <example>123e4567-e89b-12d3-a456-426614174000</example>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Player's score
    /// </summary>
    /// <example>1500</example>
    public long Score { get; set; }

    /// <summary>
    /// Player's rank in the leaderboard (1-based)
    /// </summary>
    /// <example>1</example>
    public int Rank { get; set; }
}