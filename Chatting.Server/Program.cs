using Chatting.Server;
using Chatting.Server.Data;
using Chatting.Server.Models;
using Chatting.Server.Models.Validators;
using Chatting.Server.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logPath);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(logPath, "chatting-server-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30
    )
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(builder.Configuration.GetConnectionString("ChattingDatabase")));

// Register FluentValidation validator
builder.Services.AddScoped<IValidator<CommentDto>, CommentDtoValidator>();

// Register CacheService as Singleton (keeps data in memory throughout app lifecycle)
builder.Services.AddSingleton<ICacheService, CacheService>();

// Queue comment-created notifications for background WebSocket broadcasting.
builder.Services.AddSingleton(Channel.CreateBounded<CommentMessage>(new BoundedChannelOptions(256)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false,
    AllowSynchronousContinuations = false
}));
builder.Services.AddHostedService<CommentNotificationBackgroundService>();

// Register CommentService with DbContext
// ICommentService depends on scoped services (DbContext, validators) so register it as scoped.
builder.Services.AddScoped<ICommentService>(provider =>
{
    var validator = provider.GetRequiredService<IValidator<CommentDto>>();
    var context = provider.GetRequiredService<AppDbContext>();
    var cacheService = provider.GetRequiredService<ICacheService>();
    var notificationChannel = provider.GetRequiredService<Channel<CommentMessage>>();
    return new CommentService(validator, context, cacheService, notificationChannel);
});

var app = builder.Build();

// Run pending database migrations and ensure database is created
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create/update the database schema through EF Core migrations.
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully.");
    }
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while applying database migrations.");
    throw; // Re-throw so we can see the error
}

// Load comments into cache before starting the application
try
{
    using (var scope = app.Services.CreateScope())
    {
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.LoadCommentsAsync();
        Log.Information("Comments loaded into cache successfully.");
    }
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while loading comments into cache.");
    throw;
}

// Configure the HTTP request pipeline.

app.UseAuthorization();

// WS
app.UseWebSockets();

app.Map("/ws/comments", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    ConnectionRegistry.Add(socket); // add to your concurrent store

    try
    {
        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open)
        {
            // read full message
            var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close) break;

            ms.Seek(0, SeekOrigin.Begin);
            var text = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(text)) continue;

            // parse minimal JSON { type, requestId, ... }
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) continue;
            var type = typeEl.GetString();

            if (type == "validateCaptcha")
            {
                // expected payload: { type: "validateCaptcha", requestId: "...", captchaId: "...", answer: "..." }
                var requestId = root.GetProperty("requestId").GetString();
                var captchaIdStr = root.GetProperty("captchaId").GetString();
                var answer = root.GetProperty("answer").GetString();

                var ok = false;
                try
                {
                    if (Guid.TryParse(captchaIdStr, out var guid))
                    {
                        ok = CaptchaStore.Validate(guid, answer);
                    }
                }
                catch
                {
                    ok = false;
                }

                var response = new
                {
                    type = "validateCaptchaResult",
                    requestId,
                    ok,
                    message = ok ? "OK" : "InvalidCaptcha"
                };
                var payload = JsonSerializer.Serialize(response);
                var outBytes = Encoding.UTF8.GetBytes(payload);
                await socket.SendAsync(new ArraySegment<byte>(outBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            // handle other message types as needed...

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
            // Optionally process client messages (subscribe/unsubscribe)

        }
    }
    finally
    {
        ConnectionRegistry.Remove(socket);
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
    }
});

// controllers
app.MapControllers();

app.Run();
