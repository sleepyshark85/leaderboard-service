namespace PS.LeaderboardAPI.Data;

public class ScoreSubmission
{
    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public long Score { get; private set; }

    /// <summary>
    /// Timestamp when this score was submitted
    /// </summary>
    public DateTime SubmittedAt { get; private set; }

    private ScoreSubmission() { }

    internal ScoreSubmission(Guid playerId, long score)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("Player ID cannot be empty", nameof(playerId));
        
        if (score < 0)
            throw new ArgumentException("Score cannot be negative", nameof(score));

        PlayerId = playerId;
        Score = score;
        SubmittedAt = DateTime.UtcNow;
    }
}