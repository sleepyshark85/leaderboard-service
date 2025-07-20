using Microsoft.EntityFrameworkCore;
using PS.LeaderboardAPI.Data;

public interface IPlayerRepository
{
    /// <summary>
    /// Gets a player by ID without loading score submissions
    /// </summary>
    /// <param name="playerId">The player's unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player if found, null otherwise</returns>
    Task<Player?> GetAsync(Guid playerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a subset of players with only basic information
    /// </summary>
    /// <param name="count">Number of players</param>
    /// <param name="page">Page number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A subset of players, null otherwise</returns>
    Task<IEnumerable<Player>?> GetRangeAsync(int count, int page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a player by ID including all score submissions
    /// </summary>
    /// <param name="playerId">The player's unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Player with score submissions if found, null otherwise</returns>
    Task<Player?> GetWithScoresAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new player to the repository
    /// </summary>
    /// <param name="player">The player to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The added player</returns>
    Task<Player> AddAsync(Player player, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing player
    /// </summary>
    /// <param name="player">The player to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated player</returns>
    Task<Player> UpdateAsync(Player player, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all players' current scores to zero
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of players affected</returns>
    Task<int> ResetAllScoresAsync(CancellationToken cancellationToken = default);
}

public class PlayerRepository : IPlayerRepository
{
    private readonly LeaderboardDbContext _context;
    private readonly ILogger<PlayerRepository> _logger;

    public PlayerRepository(LeaderboardDbContext context, ILogger<PlayerRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Player?> GetAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting player {PlayerId} without score submissions", playerId);

        return await _context.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Player>?> GetRangeAsync(int count, int page, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting players by page: {PageNumber}/{PageSize}", page, count);

        if (page < 1)
        {
            throw new ArgumentException("Page number must be greater than 0", nameof(page));
        }

        if (count < 1)
        {
            throw new ArgumentException("Page size must be greater than 0", nameof(count));
        }

        var skipCount = (page - 1) * count;

        var players = await _context.Players
            .AsNoTracking()
            .OrderByDescending(p => p.CurrentScore)
            .Skip(skipCount)
            .Take(count)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} players from page {PageNumber}", players.Count, page);

        return players;
    }

    /// <inheritdoc />
    public async Task<Player?> GetWithScoresAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting player {PlayerId} with score submissions", playerId);

        return await _context.Players
            .Include(p => p.ScoreSubmissions.OrderByDescending(s => s.SubmittedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Player> AddAsync(Player player, CancellationToken cancellationToken = default)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        _logger.LogDebug("Adding new player {PlayerId}", player.Id);

        var entry = await _context.Players.AddAsync(player, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully added player {PlayerId}", player.Id);
        return entry.Entity;
    }

    /// <inheritdoc />
    public async Task<Player> UpdateAsync(Player player, CancellationToken cancellationToken = default)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        _logger.LogDebug("Updating player {PlayerId}", player.Id);

        _context.Players.Update(player);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully updated player {PlayerId}", player.Id);
        return player;
    }

    /// <inheritdoc />
    public async Task<int> ResetAllScoresAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting all player scores to zero");

        try
        {
            // Use bulk update for performance
            var affectedRows = await _context.Players
                .Where(p => p.CurrentScore > 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.CurrentScore, 0)
                    .SetProperty(p => p.LastUpdatedAt, DateTime.UtcNow),
                    cancellationToken);

            _logger.LogInformation("Successfully reset scores for {Count} players", affectedRows);

            return affectedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset player scores");
            throw;
        }
    }
}