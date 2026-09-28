namespace CrownBuilders.Services;

public sealed record CheckoutReturn(string Result);

public sealed class CheckoutReturnService
{
    public event EventHandler<CheckoutReturn>? ReturnReceived;

    public CheckoutReturn? PendingReturn { get; private set; }

    public void Handle(string? uriText)
    {
        if (!TryParse(uriText, out var checkoutReturn) || checkoutReturn is null)
        {
            return;
        }

        PendingReturn = checkoutReturn;
        ReturnReceived?.Invoke(this, checkoutReturn);
    }

    public CheckoutReturn? GetPending()
    {
        return PendingReturn;
    }

    public void ClearPending()
    {
        PendingReturn = null;
    }

    public static bool TryParse(string? uriText, out CheckoutReturn? checkoutReturn)
    {
        checkoutReturn = null;

        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "crownbuilders", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "checkout", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var result = uri.AbsolutePath.Trim('/');
        if (!string.Equals(result, "success", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(result, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        checkoutReturn = new CheckoutReturn(result.ToLowerInvariant());
        return true;
    }
}
