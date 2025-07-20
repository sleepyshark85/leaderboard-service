using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace PS.LeaderboardAPI.Tests;

public class SimpleSystemTests
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string _apiBaseUrl = "http://localhost:5108";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    [Fact]
    public async Task SubmitScore_Should_Work()
    {
        // Arrange
        var player = await AddPlayerAsync();

        var score = 1500;
        var request = new { PlayerId = player.PlayerId, Score = score };
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/submit", content);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var responseStream = await response.Content.ReadAsStreamAsync();
        var result = JsonSerializer.Deserialize<SubmitScoreResponse>(responseStream, _jsonOptions);

        result.Should().NotBeNull();
        result!.PlayerScore.Should().Be(score);
        result.PlayerRank.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLeaderboard_Should_Work()
    {
        // Arrange - First submit a score
        var player = await AddPlayerAsync();

        var score = 2000;
        var submitRequest = new { PlayerId = player.PlayerId, Score = score };
        var submitJson = JsonSerializer.Serialize(submitRequest, _jsonOptions);
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");

        await _httpClient.PostAsync($"{_apiBaseUrl}/api/submit", submitContent);

        // Act
        var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/leaderboard?playerId={player.PlayerId}");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var responseStream = await response.Content.ReadAsStreamAsync();
        var result = JsonSerializer.Deserialize<GetLeaderboardResponse>(responseStream, _jsonOptions);

        result.Should().NotBeNull();
        result!.PlayerScore.Should().Be(score);
    }

    [Fact]
    public async Task Reset_Should_Work()
    {
        // Act
        var response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/reset", null);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        var responseStream = await response.Content.ReadAsStreamAsync();
        var result = JsonSerializer.Deserialize<ResetScoresResponse>(responseStream, _jsonOptions);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    private async Task<AddPlayerResponse> AddPlayerAsync()
    {
        var request = new { Name = Guid.NewGuid().ToString() };
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/players", content);

        response.IsSuccessStatusCode.Should().BeTrue();

        var responseStream = await response.Content.ReadAsStreamAsync();
        var result = JsonSerializer.Deserialize<AddPlayerResponse>(responseStream, _jsonOptions);

        result.Should().NotBeNull();

        return result;
    }
}

public class AddPlayerResponse
{
    public Guid PlayerId { get; set; }
    public long CurrentScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

public class SubmitScoreResponse
{
    public int PlayerRank { get; set; }
    public long PlayerScore { get; set; }
    public bool Degraded { get; set; }
}

public class GetLeaderboardResponse
{
    public int PlayerRank { get; set; }
    public long PlayerScore { get; set; }
    public bool Degraded { get; set; }
}

public class ResetScoresResponse
{
    public bool Success { get; set; }
    public int PlayersAffected { get; set; }
}