using System.Globalization;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;
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
    [HttpPost("crear-sesion-portal")]
    public async Task<IActionResult> CrearSesionPortal(CancellationToken cancellationToken)
    {
        try
        {
            var authenticatedUser = AuthenticatedUserAccessor.FromPrincipal(User);
            var compania = await _context.Companias
                .SingleOrDefaultAsync(c => c.Id == authenticatedUser.UserId, cancellationToken);

            if (compania is null)
            {
                return NotFound(new { error = "Compañía no encontrada." });
            }

            StripeConfiguration.ApiKey = _stripeOptions.SecretKey;

            if (string.IsNullOrWhiteSpace(compania.StripeCustomerId))
            {
                var customerOptions = new CustomerCreateOptions
                {
                    Email = compania.Correo,
                    Name = compania.NombreEmpresa,
                    Metadata = new Dictionary<string, string>
                    {
                        ["company_id"] = compania.Id.ToString(CultureInfo.InvariantCulture)
                    }
                };

                var customerService = new CustomerService();
                var customer = await customerService.CreateAsync(customerOptions, cancellationToken: cancellationToken);

                compania.StripeCustomerId = customer.Id;
                await _context.SaveChangesAsync(cancellationToken);
            }

            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = compania.StripeCustomerId,
                ReturnUrl = _stripeOptions.PublicBaseUrl
            };

            var service = new Stripe.BillingPortal.SessionService();
            Stripe.BillingPortal.Session session = await service.CreateAsync(options, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(session.Url))
            {
                throw new StripeException("Stripe no devolvió una URL del Portal de Clientes.");
            }

            return Ok(new PortalSessionResponse(session.Url));
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe rechazó la creación de la sesión del portal.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Stripe no pudo generar la sesión para la gestión de tarjetas." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error inesperado creando sesión del portal.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "No se pudo abrir el portal de gestión de tarjetas." });
        }
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
            var sessionOptions = new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                CustomerEmail = compania.Correo,
                ClientReferenceId = compania.Id.ToString(CultureInfo.InvariantCulture),
                SuccessUrl = BuildCheckoutReturnUrl(_stripeOptions.CheckoutSuccessUrl, "success"),
                CancelUrl = BuildCheckoutReturnUrl(_stripeOptions.CheckoutCancelUrl, "cancel"),
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

            var session = await new Stripe.Checkout.SessionService().CreateAsync(
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

    [Authorize(Roles = "compania")]
    [HttpPost("sync-payment-method")]
    public async Task<IActionResult> SyncPaymentMethod(CancellationToken cancellationToken)
    {
        try
        {
            var authenticatedUser = AuthenticatedUserAccessor.FromPrincipal(User);
            var compania = await _context.Companias
                .SingleOrDefaultAsync(c => c.Id == authenticatedUser.UserId, cancellationToken);

            if (compania is null)
            {
                return NotFound(new { error = "Compañía no encontrada." });
            }

            if (string.IsNullOrWhiteSpace(compania.StripeSubscriptionId) &&
                string.IsNullOrWhiteSpace(compania.StripeCheckoutSessionId))
            {
                return Ok(new { sincronizado = false });
            }

            await PersistCardSummaryAsync(
                compania,
                compania.StripeSubscriptionId,
                null,
                null,
                compania.StripeCheckoutSessionId,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                sincronizado = !string.IsNullOrWhiteSpace(compania.StripeCardLast4),
                compania.StripeCardholderName,
                compania.StripeCardBrand,
                compania.StripeCardLast4,
                compania.StripeCardExpMonth,
                compania.StripeCardExpYear
            });
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "No se pudo sincronizar el método de pago de la compañía.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Stripe no devolvió los datos resumidos de la tarjeta." });
        }
    }

    [AllowAnonymous]
    [HttpGet("checkout-return/{result}")]
    public IActionResult CheckoutReturn(string result)
    {
        if (!string.Equals(result, "success", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(result, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return Redirect($"crownbuilders://checkout/{result.ToLowerInvariant()}");
    }

    private string BuildCheckoutReturnUrl(string configuredUrl, string result)
    {
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri) &&
            string.Equals(configuredUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return configuredUri.ToString();
        }

        return $"{_stripeOptions.PublicBaseUrl.TrimEnd('/')}/api/Stripe/checkout-return/{result}";
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
        if (stripeEvent.Data.Object is not Stripe.Checkout.Session session ||
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
        await PersistCardSummaryAsync(
            compania,
            session.SubscriptionId,
            session.PaymentIntentId,
            session.CustomerDetails?.Name,
            session.Id,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistCardSummaryAsync(
        Compania compania,
        string? subscriptionId,
        string? paymentIntentId,
        string? fallbackCardholderName,
        string? checkoutSessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            PaymentMethod? paymentMethod = null;
            StripeConfiguration.ApiKey = _stripeOptions.SecretKey;

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                var subscription = await new SubscriptionService().GetAsync(
                    subscriptionId,
                    new SubscriptionGetOptions
                    {
                        Expand =
                        [
                            "default_payment_method",
                            "latest_invoice.default_payment_method"
                        ]
                    },
                    cancellationToken: cancellationToken);

                paymentMethod = subscription.DefaultPaymentMethod ?? subscription.LatestInvoice?.DefaultPaymentMethod;
                if (paymentMethod is null && !string.IsNullOrWhiteSpace(subscription.DefaultPaymentMethodId))
                {
                    paymentMethod = await new PaymentMethodService().GetAsync(
                        subscription.DefaultPaymentMethodId,
                        cancellationToken: cancellationToken);
                }

                if (paymentMethod is null && !string.IsNullOrWhiteSpace(subscription.LatestInvoice?.DefaultPaymentMethodId))
                {
                    paymentMethod = await new PaymentMethodService().GetAsync(
                        subscription.LatestInvoice.DefaultPaymentMethodId,
                        cancellationToken: cancellationToken);
                }
            }

            if (paymentMethod is null && !string.IsNullOrWhiteSpace(paymentIntentId))
            {
                var paymentIntent = await new PaymentIntentService().GetAsync(
                    paymentIntentId,
                    new PaymentIntentGetOptions
                    {
                        Expand = ["payment_method"]
                    },
                    cancellationToken: cancellationToken);
                paymentMethod = paymentIntent.PaymentMethod;
            }

            if (!string.IsNullOrWhiteSpace(checkoutSessionId) &&
                (paymentMethod is null || string.IsNullOrWhiteSpace(fallbackCardholderName)))
            {
                var checkoutSession = await new Stripe.Checkout.SessionService().GetAsync(
                    checkoutSessionId,
                    new SessionGetOptions
                    {
                        Expand =
                        [
                            "payment_intent.payment_method",
                            "subscription.default_payment_method"
                        ]
                    },
                    cancellationToken: cancellationToken);

                paymentMethod ??= checkoutSession.PaymentIntent?.PaymentMethod ??
                    checkoutSession.Subscription?.DefaultPaymentMethod;
                fallbackCardholderName ??= checkoutSession.CustomerDetails?.Name;
            }

            if (paymentMethod is null ||
                !string.Equals(paymentMethod.Type, "card", StringComparison.OrdinalIgnoreCase) ||
                paymentMethod.Card is null)
            {
                return;
            }

            var cardholderName = paymentMethod.BillingDetails?.Name;
            if (string.IsNullOrWhiteSpace(cardholderName))
            {
                cardholderName = fallbackCardholderName;
            }

            compania.StripeCardholderName = string.IsNullOrWhiteSpace(cardholderName)
                ? null
                : cardholderName.Trim();
            compania.StripeCardBrand = paymentMethod.Card.DisplayBrand ?? paymentMethod.Card.Brand;
            compania.StripeCardLast4 = paymentMethod.Card.Last4;
            compania.StripeCardExpMonth = checked((int)paymentMethod.Card.ExpMonth);
            compania.StripeCardExpYear = checked((int)paymentMethod.Card.ExpYear);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo guardar el resumen seguro de la tarjeta para el Checkout {CheckoutSessionId}.",
                checkoutSessionId);
        }
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
        if (stripeEvent.Type == EventTypes.CustomerSubscriptionUpdated)
        {
            await PersistCardSummaryAsync(
                compania,
                subscription.Id,
                null,
                null,
                null,
                cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CheckoutSessionResponse(string CheckoutUrl);
public sealed record PortalSessionResponse(string Url);