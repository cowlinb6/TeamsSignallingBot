using System.Text.Json;
using Microsoft.Extensions.Options;
using SignalingBot.Configuration;
using SignalingBot.Models;
using SignalingBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection("Bot"))
    .ValidateOnStart();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IGraphTokenProvider, GraphTokenProvider>();
builder.Services.AddSingleton<INotificationAuthenticator, NotificationAuthenticator>();
builder.Services.AddSingleton<ICallStateTracker, CallStateTracker>();
builder.Services.AddSingleton<ICallService, CallService>();
builder.Services.AddSingleton<INotificationProcessor, NotificationProcessor>();

var app = builder.Build();

// Pre-warm the Graph token so the first call:answer beats the ~5s policy deadline.
_ = app.Services.GetRequiredService<IGraphTokenProvider>()
    .WarmUpAsync()
    .ContinueWith(t =>
    {
        if (t.IsFaulted)
        {
            app.Logger.LogWarning(t.Exception, "Token warm-up failed; will retry on first use.");
        }
    });

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Calling notification webhook. Graph also probes this endpoint, and incoming
// policy calls arrive here; we must validate the token, ACK fast, and answer.
app.MapPost("/api/calling", async (
    HttpRequest request,
    INotificationAuthenticator authenticator,
    INotificationProcessor processor,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    var logger = loggerFactory.CreateLogger("CallingWebhook");

    if (!await authenticator.ValidateAsync(request.Headers.Authorization, ct))
    {
        return Results.Unauthorized();
    }

    CommsNotifications? notifications;
    try
    {
        notifications = await JsonSerializer.DeserializeAsync<CommsNotifications>(
            request.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            ct);
    }
    catch (JsonException ex)
    {
        logger.LogWarning(ex, "Could not parse notification payload.");
        return Results.BadRequest();
    }

    if (notifications is null || notifications.Value.Count == 0)
    {
        return Results.Accepted();
    }

    await processor.ProcessAsync(notifications, ct);
    return Results.Accepted();
});

app.Run();

