using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Chatting.Server.Models;

namespace Chatting.Server
{
    public static class ConnectionRegistry
    {
        private static readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

        public static void Add(WebSocket socket) => _sockets.TryAdd(Guid.NewGuid().ToString(), socket);
        public static void Remove(WebSocket socket)
        {
            var kv = _sockets.FirstOrDefault(kv => kv.Value == socket);
            if (kv.Key != null) _sockets.TryRemove(kv.Key, out _);
        }

        public static async Task BroadcastCommentCreatedAsync(CommentMessage message, CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new { type = "commentCreated", model = message }, 
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var bytes = Encoding.UTF8.GetBytes(payload);
            var seg = new ArraySegment<byte>(bytes);
            foreach (var ws in _sockets.Values.ToList())
            {
                if (ws.State != WebSocketState.Open) continue;
                try { await ws.SendAsync(seg, WebSocketMessageType.Text, true, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { /* handle / remove failed sockets */ }
            }
        }
    }
}
