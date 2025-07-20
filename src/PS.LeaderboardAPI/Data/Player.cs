namespace PS.LeaderboardAPI.Data;

public class Player
{
    public Guid Id { get; private set; }
    public string Name { get; set; }

    /// <summary>
    /// Current best score for the player
    /// </summary>
    public long CurrentScore { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime LastUpdatedAt { get; private set; }

    private readonly List<ScoreSubmission> _scoreSubmissions = new();
    public IReadOnlyCollection<ScoreSubmission> ScoreSubmissions => _scoreSubmissions;

    public Player(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Missing player name", nameof(name));

        Name = name;
        CurrentScore = 0;
        CreatedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Submits a new score for the player.
    /// Only updates the current score if the new score is higher.
    /// Always creates a score submission record for audit purposes.
    /// </summary>
    /// <param name="score">The score to submit</param>
    /// <returns>A ScoreSubmission entity representing this submission</returns>
    /// <exception cref="ArgumentException">Thrown when score is negative</exception>
    public ScoreSubmission SubmitScore(long score)
    {
        if (score < 0)
            throw new ArgumentException("Score cannot be negative", nameof(score));

        var submission = new ScoreSubmission(Id, score);
        _scoreSubmissions.Add(submission);

        // Update current score only if new score is higher
        if (score > CurrentScore)
        {
            CurrentScore = score;
            LastUpdatedAt = DateTime.UtcNow;
        }

        return submission;
    }

    /// <summary>
    /// Resets the player's current score to zero.
    /// </summary>
    public void ResetScore()
    {
        CurrentScore = 0;
        LastUpdatedAt = DateTime.UtcNow;
    }
}