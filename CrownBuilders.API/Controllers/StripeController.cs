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
                // 1. Crear el cliente en Stripe utilizando los datos enviados desde la app móvil
                var customerOptions = new CustomerCreateOptions
                {
                    Name = request.NombreCompania,
                    Email = request.EmailCompania,
                    Phone = request.TelefonoCompania
                };
                var customerService = new CustomerService();
                Customer customer = await customerService.CreateAsync(customerOptions);

                // 2. Crear la suscripción vinculada al nuevo cliente y al precio configurado
                var options = new SubscriptionCreateOptions
                {
                    Customer = customer.Id,
                    Items = new List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            Price = _configuration["Stripe:SubscriptionPriceId"],
                        },
                    },
                    PaymentBehavior = "default_incomplete",
                    Expand = new List<string> { "latest_invoice.payment_intent" }
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
        public string NombreCompania { get; set; }
        public string EmailCompania { get; set; }
        public string TelefonoCompania { get; set; }
    }
}