using PodcastFeedApi.Models;
using PodcastFeedApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<PodcastFeedsConfig>(
    builder.Configuration.GetSection("PodcastFeeds"));
builder.Services.AddSingleton<PodcastFeedService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
