using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace E_Dnevnik_API.ScrapingServices
{
    // singleton koji drži aktivne sesije - svaki korisnik dobiva token koji mapira na e-dnevnik cookije
    // ne čuvamo lozinke nigdje - samo kolačiće koji su dobiveni nakon uspješnog logina
    public class SessionStore
    {
        private readonly record struct Session(CookieContainer Cookies, DateTime ExpiresAt, string Email);

        private readonly ConcurrentDictionary<string, Session> _sessions = new();

        // sesija traje 24h od zadnjeg korištenja
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

        public string CreateSession(CookieContainer cookies, string email)
        {
            // čistimo istekle sesije pri svakom novom loginu da se memorija ne puni
            CleanupExpired();

            // 32 slučajna bajta = 256-bitni token, kriptografski siguran
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _sessions[token] = new Session(cookies, DateTime.UtcNow.Add(SessionLifetime), email);
            return token;
        }

        // vraća cookie container ako je token valjan, null ako je istekao ili ne postoji
        public CookieContainer? GetCookies(string token)
        {
            if (!_sessions.TryGetValue(token, out var session))
                return null;

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _sessions.TryRemove(token, out _);
                return null;
            }

            // klizno istjecanje - svaki poziv produlji sesiju za još 24h
            _sessions[token] = session with
            {
                ExpiresAt = DateTime.UtcNow.Add(SessionLifetime),
            };
            return session.Cookies;
        }

        // vraća email za token ako je valjan, null ako je istekao ili ne postoji
        public string? GetEmailByToken(string token)
        {
            if (!_sessions.TryGetValue(token, out var session))
                return null;

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _sessions.TryRemove(token, out _);
                return null;
            }

            return session.Email;
        }

        // poziva se pri odjavi - briše sesiju odmah
        public void RemoveSession(string token) => _sessions.TryRemove(token, out _);

        // vraća sve aktivne sesije - koristi NewDataRefreshService za background refresh
        public IEnumerable<(string Token, CookieContainer Cookies)> GetAllActiveSessions()
        {
            var now = DateTime.UtcNow;
            return _sessions
                .Where(kvp => kvp.Value.ExpiresAt >= now)
                .Select(kvp => (kvp.Key, kvp.Value.Cookies))
                .ToList();
        }

        private void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            foreach (
                var key in _sessions.Where(kvp => kvp.Value.ExpiresAt < now).Select(kvp => kvp.Key)
            )
                _sessions.TryRemove(key, out _);
        }
    }
}
