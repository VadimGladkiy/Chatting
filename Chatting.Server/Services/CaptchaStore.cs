using System;
using System.Collections.Concurrent;

namespace Chatting.Server.Services
{
    // Very small in-memory captcha store for demo purposes
    public static class CaptchaStore
    {
        private static readonly ConcurrentDictionary<Guid, string> _answers = new();
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static (Guid id, string question) CreateArithmeticChallenge()
        {
            var rnd = new Random();
            var str =  new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[rnd.Next(s.Length)]).ToArray());
            var id = Guid.NewGuid();
            var question = str;
            _answers[id] = str;
            return (id, question);
        }

        public static bool Validate(Guid id, string answer)
        {
            if (id == Guid.Empty)
            { 
                return false;
            }

            if (!_answers.TryGetValue(id, out var real)) 
            {
                return false;
            }

            var ok = string.Equals(real, answer?.Trim(), StringComparison.OrdinalIgnoreCase);
            
            if (ok)
            {
                // one-time token
                _answers.TryRemove(id, out _);
            }

            return ok;
        }
    }
}
