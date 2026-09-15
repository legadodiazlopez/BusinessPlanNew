namespace CrownBuilders.API.Services;

public sealed record MessageValidationResult(bool IsValid, string? Error);

public static class MessageContentValidator
{
    public const int MaxLength = 4_000;

    public static MessageValidationResult Validate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new MessageValidationResult(false, "El texto del mensaje es obligatorio.");
        }

        return text.Trim().Length <= MaxLength
            ? new MessageValidationResult(true, null)
            : new MessageValidationResult(false, $"El texto del mensaje no puede superar {MaxLength} caracteres.");
    }
}
