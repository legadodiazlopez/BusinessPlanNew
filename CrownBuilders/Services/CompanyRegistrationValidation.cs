using System.Globalization;
using System.Text.RegularExpressions;

namespace CrownBuilders.Services;

public static class CompanyRegistrationValidation
{
    private static readonly Regex EmailPattern = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailPattern.IsMatch(email.Trim());

    public static bool IsValidPhone(string? phone) =>
        HasOnlyDigits(phone, 10, 10);

    public static bool IsValidCvc(string? cvc) =>
        HasOnlyDigits(cvc, 3, 3);

    public static bool IsValidCardNumber(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return false;
        }

        var digits = cardNumber.Trim();
        if (digits.Length is < 13 or > 19 || !digits.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        var shouldDouble = false;
        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var digit = digits[index] - '0';
            if (shouldDouble)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            shouldDouble = !shouldDouble;
        }

        return sum % 10 == 0;
    }

    public static bool IsValidExpiration(string? expiration, DateTime currentDate)
    {
        if (!DateTime.TryParseExact(
                expiration,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var selectedMonth))
        {
            return false;
        }

        var currentMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
        return selectedMonth > currentMonth;
    }

    public static string ToStoredExpiration(string expiration)
    {
        if (!DateTime.TryParseExact(
                expiration,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var selectedMonth))
        {
            return string.Empty;
        }

        return selectedMonth.ToString("MM/yy", CultureInfo.InvariantCulture);
    }

    private static bool HasOnlyDigits(string? value, int minimumLength, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length >= minimumLength &&
        value.Length <= maximumLength &&
        value.All(char.IsDigit);
}
