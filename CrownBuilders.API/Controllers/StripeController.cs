using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace CrownBuilders.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StripeController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public StripeController(IConfiguration configuration)
        {
            _configuration = configuration;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        [HttpPost("create-subscription")]
        public async Task<IActionResult> CreateSubscription([FromBody] SubscriptionRequest request)
        {
            try
            {
                var options = new SubscriptionCreateOptions
                {
                    Customer = request.CustomerId,
                    Items = new List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            Price = _configuration["Stripe:SubscriptionPriceId"],
                        },
                    },
                    PaymentBehavior = "default_incomplete",
                };

                var service = new SubscriptionService();
                Subscription subscription = await service.CreateAsync(options);

                return Ok(new
                {
                    subscriptionId = subscription.Id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class SubscriptionRequest
    {
        public string CustomerId{ get; set; }
        
        }

    }