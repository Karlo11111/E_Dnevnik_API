using E_Dnevnik_API.Database;
using E_Dnevnik_API.ScrapingServices;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class PaymentController : ApiBaseController
    {
        private const string PriceId = "price_1TRFoiQGIIBW5gARkmRIihj7";

        private readonly AppDbContext _db;
        private readonly StripeClient _stripe;
        private readonly FirestoreDb? _firestore;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            SessionStore sessionStore,
            AppDbContext db,
            StripeClient stripe,
            IServiceProvider services,
            ILogger<PaymentController> logger
        ) : base(sessionStore)
        {
            _db = db;
            _stripe = stripe;
            _firestore = services.GetService<FirestoreDb>();
            _logger = logger;
        }

        [HttpPost("CreateSubscription")]
        public async Task<IActionResult> CreateSubscription()
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            try
            {
                var customerService = new CustomerService(_stripe);
                var existing = await customerService.ListAsync(
                    new CustomerListOptions { Email = email, Limit = 1 }
                );
                var customer =
                    existing.Data.FirstOrDefault()
                    ?? await customerService.CreateAsync(
                        new CustomerCreateOptions { Email = email }
                    );

                var subscriptionService = new SubscriptionService(_stripe);
                var subscription = await subscriptionService.CreateAsync(
                    new SubscriptionCreateOptions
                    {
                        Customer = customer.Id,
                        Items = [new SubscriptionItemOptions { Price = PriceId }],
                        PaymentBehavior = "default_incomplete",
                        Expand = ["latest_invoice"],
                    }
                );

                // In Stripe.net v51, Invoice.PaymentIntent was replaced by Invoice.ConfirmationSecret
                var clientSecret = subscription.LatestInvoice?.ConfirmationSecret?.ClientSecret;
                return Ok(new { clientSecret, subscriptionId = subscription.Id });
            }
            catch (StripeException ex)
            {
                _logger.LogError("Stripe error in CreateSubscription: {Message}", ex.Message);
                return StatusCode(500, "Greška pri pokretanju plaćanja.");
            }
        }

        [HttpPost("Confirm")]
        public async Task<IActionResult> Confirm([FromBody] ConfirmRequest request)
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            if (string.IsNullOrEmpty(request.SubscriptionId))
                return BadRequest("subscriptionId je obavezan.");

            Subscription subscription;
            try
            {
                var subscriptionService = new SubscriptionService(_stripe);
                subscription = await subscriptionService.GetAsync(request.SubscriptionId);
            }
            catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
            {
                return NotFound("Pretplata nije pronađena.");
            }
            catch (StripeException ex)
            {
                _logger.LogError("Stripe error in Confirm: {Message}", ex.Message);
                return StatusCode(500, "Greška pri provjeri pretplate.");
            }

            if (subscription.Status != "active" && subscription.Status != "trialing")
                return NotFound("Pretplata nije aktivna.");

            if (_firestore != null)
            {
                try
                {
                    var doc = _firestore.Collection("studentProfiles").Document(email);
                    await doc.SetAsync(
                        new Dictionary<string, object>
                        {
                            { "odlikasPlus", true },
                            { "purchasedAt", Timestamp.GetCurrentTimestamp() },
                        },
                        SetOptions.MergeAll
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError("Firestore write failed in Confirm: {Message}", ex.Message);
                }
            }

            var cache = await _db.StudentCache.FindAsync(email);
            if (cache != null)
            {
                cache.IsOdlikasPlus = true;
                cache.OdlikasPlusSince = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        public record ConfirmRequest(string SubscriptionId);
    }
}
