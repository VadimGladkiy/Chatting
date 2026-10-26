using System.Threading.Channels;
using Chatting.Server.Models;

namespace Chatting.Server.Services;

public sealed class CommentNotificationBackgroundService : BackgroundService
{
    private readonly Channel<CommentMessage> _notificationChannel;
    private readonly ILogger<CommentNotificationBackgroundService> _logger;

    public CommentNotificationBackgroundService(
        Channel<CommentMessage> notificationChannel,
        ILogger<CommentNotificationBackgroundService> logger)
    {
        _notificationChannel = notificationChannel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _notificationChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ConnectionRegistry.BroadcastCommentCreatedAsync(message, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to broadcast a queued comment notification.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
    }
}
