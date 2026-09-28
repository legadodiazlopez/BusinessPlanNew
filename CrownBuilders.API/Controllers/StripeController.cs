using System.Globalization;
using CrownBuilders.API.Data;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CrownBuilders.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class StripeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<StripeController> _logger;

    public StripeController(
        ApplicationDbContext context,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeController> logger)
    {
        _context = context;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    [Authorize(Roles = "compania")]
    [HttpPost("create-checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession(CancellationToken cancellationToken)
    {
        try
        {
            _stripeOptions.ValidateForCheckout();
            var authenticatedUser = AuthenticatedUserAccessor.FromPrincipal(User);
            var compania = await _context.Companias
                .SingleOrDefaultAsync(c => c.Id == authenticatedUser.UserId, cancellationToken);

            if (compania is null)
            {
                return NotFound(new { error = "Compañía no encontrada." });
            }

            StripeConfiguration.ApiKey = _stripeOptions.SecretKey;
            var sessionOptions = new SessionCreateOptions
            {
                Mode = "subscription",
                CustomerEmail = compania.Correo,
                ClientReferenceId = compania.Id.ToString(CultureInfo.InvariantCulture),
                SuccessUrl = _stripeOptions.CheckoutSuccessUrl,
                CancelUrl = _stripeOptions.CheckoutCancelUrl,
                Locale = "es",
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = _stripeOptions.SubscriptionPriceId,
                        Quantity = 1
                    }
                ],
                Metadata = new Dictionary<string, string>
                {
                    ["company_id"] = compania.Id.ToString(CultureInfo.InvariantCulture)
                }
            };

            var session = await new SessionService().CreateAsync(
                sessionOptions,
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(session.Url))
            {
                throw new StripeException("Stripe no devolvió una URL de Checkout.");
            }

            compania.StripeCheckoutSessionId = session.Id;
            compania.StripePaymentStatus = "pending";
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new CheckoutSessionResponse(session.Url));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Configuración de Stripe Checkout incompleta.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "El pago no está disponible porque falta configuración de Stripe." });
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe rechazó la creación de Checkout.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Stripe no pudo crear la sesión de pago." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error inesperado creando Checkout.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "No se pudo iniciar el pago." });
        }
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new { error = "Firma de Stripe ausente." });
        }

        Event stripeEvent;
        try
        {
            _stripeOptions.ValidateForWebhook();
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature.ToString(),
                _stripeOptions.WebhookSecret);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Configuración del webhook de Stripe incompleta.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "El webhook de Stripe no está configurado." });
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "Webhook de Stripe con firma inválida.");
            return BadRequest(new { error = "Firma de Stripe inválida." });
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await ProcessCheckoutCompletedAsync(stripeEvent, cancellationToken);
                break;
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
            case EventTypes.CustomerSubscriptionPaused:
            case EventTypes.CustomerSubscriptionResumed:
                await ProcessSubscriptionChangedAsync(stripeEvent, cancellationToken);
                break;
        }

        return Ok(new { received = true });
    }

    private async Task ProcessCheckoutCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Session session ||
            !session.Metadata.TryGetValue("company_id", out var companyIdValue) ||
            !int.TryParse(companyIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var companyId))
        {
            _logger.LogWarning("Checkout completado sin metadata company_id. EventId: {EventId}", stripeEvent.Id);
            return;
        }

        var compania = await _context.Companias
            .SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (compania is null)
        {
            _logger.LogWarning("Checkout recibido para compañía inexistente. CompanyId: {CompanyId}", companyId);
            return;
        }

        compania.StripeCustomerId = session.CustomerId;
        compania.StripeSubscriptionId = session.SubscriptionId;
        compania.StripeCheckoutSessionId = session.Id;
        compania.StripePaymentStatus = session.PaymentStatus == "paid" ? "paid" : "pending";
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessSubscriptionChangedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
        {
            return;
        }

        var compania = await _context.Companias.SingleOrDefaultAsync(
            c => c.StripeSubscriptionId == subscription.Id || c.StripeCustomerId == subscription.CustomerId,
            cancellationToken);
        if (compania is null)
        {
            _logger.LogWarning("Suscripción recibida sin compañía asociada. SubscriptionId: {SubscriptionId}", subscription.Id);
            return;
        }

        compania.StripeSubscriptionId = subscription.Id;
        compania.StripePaymentStatus = subscription.Status switch
        {
            "active" or "trialing" => "paid",
            "canceled" or "unpaid" or "past_due" => "inactive",
            _ => "pending"
        };
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CheckoutSessionResponse(string CheckoutUrl);
