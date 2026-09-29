namespace CrownBuilders.API.Services;

public sealed class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string SubscriptionPriceId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string CheckoutSuccessUrl { get; set; } = string.Empty;
    public string CheckoutCancelUrl { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;

    public void ValidateForCheckout()
    {
        if (string.IsNullOrWhiteSpace(SecretKey) ||
            string.IsNullOrWhiteSpace(SubscriptionPriceId) ||
            string.IsNullOrWhiteSpace(CheckoutSuccessUrl) ||
            string.IsNullOrWhiteSpace(CheckoutCancelUrl) ||
            string.IsNullOrWhiteSpace(PublicBaseUrl))
        {
            throw new InvalidOperationException(
                "La configuración de Stripe Checkout está incompleta.");
        }

        if (!Uri.TryCreate(CheckoutSuccessUrl, UriKind.Absolute, out _) ||
            !Uri.TryCreate(CheckoutCancelUrl, UriKind.Absolute, out _) ||
            !Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var publicBaseUri) ||
            !string.Equals(publicBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Las URLs de retorno de Stripe deben ser HTTPS y absolutas.");
        }
    }

    public void ValidateForWebhook()
    {
        if (string.IsNullOrWhiteSpace(WebhookSecret))
        {
            throw new InvalidOperationException(
                "La configuración Stripe:WebhookSecret es obligatoria para el webhook.");
        }
    }
}
