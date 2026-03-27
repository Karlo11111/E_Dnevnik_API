using System.Collections.Concurrent;

namespace E_Dnevnik_API.ScrapingServices
{
    // singleton koji prati neuspješne pokušaje prijave po emailu
    // nakon 5 neuspjeha blokira email na 15 minuta
    public class LoginBruteForceProtection
    {
        private record struct AttemptRecord(int Count, DateTime BlockedUntil);

        private readonly ConcurrentDictionary<string, AttemptRecord> _attempts = new();

        private const int MaxAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        public bool IsBlocked(string email)
        {
            var key = email.ToLowerInvariant();
            if (!_attempts.TryGetValue(key, out var record))
                return false;

            if (record.BlockedUntil == default)
                return false;

            if (record.BlockedUntil > DateTime.UtcNow)
                return true;

            // lockout je istekao - čistimo zapis
            _attempts.TryRemove(key, out _);
            return false;
        }

        public void RecordFailure(string email)
        {
            var key = email.ToLowerInvariant();
            _attempts.AddOrUpdate(
                key,
                _ => new AttemptRecord(1, default),
                (_, existing) =>
                {
                    var newCount = existing.Count + 1;
                    var blockedUntil =
                        newCount >= MaxAttempts ? DateTime.UtcNow.Add(LockoutDuration) : default;
                    return new AttemptRecord(newCount, blockedUntil);
                }
            );
        }

        // poziva se nakon uspješnog logina - resetira brojač za taj email
        public void RecordSuccess(string email) =>
            _attempts.TryRemove(email.ToLowerInvariant(), out _);
    }
}
