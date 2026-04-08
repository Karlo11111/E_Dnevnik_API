using E_Dnevnik_API.Database;
using FirebaseAdmin.Messaging;

namespace E_Dnevnik_API.Services
{
    public class FcmService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<FcmService> _logger;

        public FcmService(AppDbContext db, ILogger<FcmService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task SendNotification(string email, string title, string body)
        {
            var cache = await _db.StudentCache.FindAsync(email);
            if (cache?.FcmToken == null)
            {
                _logger.LogInformation("[FCM] No token for {Email}, skipping notification.", email);
                return;
            }

            var message = new Message
            {
                Token = cache.FcmToken,
                Notification = new Notification { Title = title, Body = body }
            };

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                // Stale token — clear so we don't retry until Flutter refreshes it
                cache.FcmToken = null;
                cache.FcmTokenUpdatedAt = null;
                await _db.SaveChangesAsync();
                _logger.LogInformation("[FCM] Cleared stale token for {Email}.", email);
            }
        }
    }
}
