using PS.LeaderboardAPI.Data;
using PS.LeaderboardAPI.Features.Leaderboard;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors();
builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddLeaderboardFeature(builder.Configuration);


var app = builder.Build();

try
{
    //Ensure database is created
    await app.Services.EnsureDatabaseAsync();
    
    // Warm up cache
    await app.Services.WarmupCacheAsync();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Application failed to start");
    throw;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();
app.MapControllers();

app.Run();
