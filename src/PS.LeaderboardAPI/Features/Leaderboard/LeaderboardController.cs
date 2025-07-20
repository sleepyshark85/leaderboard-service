using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PS.LeaderboardAPI.Features.Leaderboard;

[ApiController]
[Route("api")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(
        ILeaderboardService leaderboardService,
        ILogger<LeaderboardController> logger)
    {
        _leaderboardService = leaderboardService ?? throw new ArgumentNullException(nameof(leaderboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds a new player to the leaderboard system
    /// </summary>
    /// <param name="request">Player creation request containing the player ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Added player information</returns>
    /// <response code="201">Player created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("players")]
    [ProducesResponseType(typeof(AddPlayerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AddPlayerResponse>> AddPlayer(
        [FromBody] AddPlayerRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding player with ID: {Name}", request.Name);

            var result = await _leaderboardService.AddPlayerAsync(request.Name, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to add player {Name}: {Error}", request.Name, result.ErrorMessage);
                return BadRequest(new ErrorResponse { Error = result.ErrorMessage! });
            }

            var response = new AddPlayerResponse
            {
                PlayerId = result.Player!.Id,
                CurrentScore = result.Player.CurrentScore,
                CreatedAt = result.Player.CreatedAt,
                LastUpdatedAt = result.Player.LastUpdatedAt
            };

            _logger.LogInformation("Successfully added player {Name}", request.Name);
            return CreatedAtAction(nameof(GetLeaderboard), new { playerId = response.PlayerId }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding player {Name}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Error = "An unexpected error occurred while adding the player" });
        }
    }

    /// <summary>
    /// Submits a score for a player and returns updated leaderboard information
    /// </summary>
    /// <param name="request">Score submission request containing player ID and score</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated player rank, score, and leaderboard data</returns>
    /// <response code="200">Score submitted successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="404">Player not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("submit")]
    [ProducesResponseType(typeof(SubmitScoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmitScoreResponse>> SubmitScore(
        [FromBody] SubmitScoreRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Submitting score {Score} for player {PlayerId}", request.Score, request.PlayerId);

            var result = await _leaderboardService.SubmitScoreAsync(request.PlayerId, request.Score, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to submit score for player {PlayerId}: {Error}", request.PlayerId, result.ErrorMessage);

                // Determine appropriate status code based on error
                if (result.ErrorMessage!.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ErrorResponse { Error = result.ErrorMessage });
                }

                return BadRequest(new ErrorResponse { Error = result.ErrorMessage });
            }

            var response = new SubmitScoreResponse
            {
                PlayerRank = result.PlayerRank,
                PlayerScore = result.PlayerScore,
                TopPlayers = result.TopPlayers.Select(MapToPlayerRanking).ToList(),
                NearbyPlayers = result.NearbyPlayers.Select(MapToPlayerRanking).ToList(),
                Degraded = result.Degraded
            };

            _logger.LogInformation("Successfully submitted score for player {PlayerId}, new rank: {Rank}",
                request.PlayerId, result.PlayerRank);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting score for player {PlayerId}", request.PlayerId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Error = "An unexpected error occurred while submitting the score" });
        }
    }

    /// <summary>
    /// Gets leaderboard information for a specific player
    /// </summary>
    /// <param name="playerId">The player's unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player rank, score, top players, and nearby players</returns>
    /// <response code="200">Leaderboard data retrieved successfully</response>
    /// <response code="400">Invalid player ID</response>
    /// <response code="404">Player not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("leaderboard")]
    [ProducesResponseType(typeof(GetLeaderboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetLeaderboardResponse>> GetLeaderboard(
        [FromQuery, Required] Guid playerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (playerId == Guid.Empty)
            {
                return BadRequest(new ErrorResponse { Error = "Player ID cannot be empty" });
            }

            _logger.LogInformation("Getting leaderboard for player {PlayerId}", playerId);

            var result = await _leaderboardService.GetLeaderboardAsync(playerId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to get leaderboard for player {PlayerId}: {Error}", playerId, result.ErrorMessage);

                // Determine appropriate status code based on error
                if (result.ErrorMessage!.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ErrorResponse { Error = result.ErrorMessage });
                }

                return BadRequest(new ErrorResponse { Error = result.ErrorMessage });
            }

            var response = new GetLeaderboardResponse
            {
                PlayerRank = result.PlayerRank,
                PlayerScore = result.PlayerScore,
                TopPlayers = result.TopPlayers.Select(MapToPlayerRanking).ToList(),
                NearbyPlayers = result.NearbyPlayers.Select(MapToPlayerRanking).ToList(),
                Degraded = result.Degraded
            };

            _logger.LogInformation("Successfully retrieved leaderboard for player {PlayerId}, rank: {Rank}",
                playerId, result.PlayerRank);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting leaderboard for player {PlayerId}", playerId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Error = "An unexpected error occurred while retrieving the leaderboard" });
        }
    }

    /// <summary>
    /// Resets all player scores to zero and clears the leaderboard
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Information about the reset operation</returns>
    /// <response code="200">Reset completed successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(ResetScoresResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResetScoresResponse>> ResetScores(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Resetting all player scores");

            var result = await _leaderboardService.ResetAllScoresAsync(cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to reset scores: {Error}", result.ErrorMessage);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ErrorResponse { Error = result.ErrorMessage! });
            }

            var response = new ResetScoresResponse
            {
                PlayersAffected = result.PlayersAffected,
                ResetAt = DateTime.UtcNow,
                Success = true
            };

            _logger.LogInformation("Successfully reset scores for {Count} players", result.PlayersAffected);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resetting scores");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Error = "An unexpected error occurred while resetting scores" });
        }
    }

    /// <summary>
    /// Maps application layer LeaderboardEntry to API response model
    /// </summary>
    private static PlayerRanking MapToPlayerRanking(LeaderboardEntry entry)
    {
        return new PlayerRanking
        {
            PlayerId = entry.PlayerId,
            Score = entry.Score,
            Rank = entry.Rank
        };
    }
}