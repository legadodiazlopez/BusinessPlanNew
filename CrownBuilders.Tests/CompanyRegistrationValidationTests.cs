using CrownBuilders.Services;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class CompanyRegistrationValidationTests
{
    [Theory]
    [InlineData("company@example.com")]
    [InlineData("nombre.apellido+registro@dominio.com")]
    public void EmailValido_AceptaFormatoCorrecto(string email)
    {
        Assert.True(CompanyRegistrationValidation.IsValidEmail(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("company@")]
    [InlineData("company.example.com")]
    public void EmailValido_RechazaFormatoIncorrecto(string email)
    {
        Assert.False(CompanyRegistrationValidation.IsValidEmail(email));
    }

    [Theory]
    [InlineData("8097283912")]
    public void TelefonoValido_ExigeDiezDigitos(string phone)
    {
        Assert.True(CompanyRegistrationValidation.IsValidPhone(phone));
        Assert.False(CompanyRegistrationValidation.IsValidPhone("809728391"));
        Assert.False(CompanyRegistrationValidation.IsValidPhone("809728391a"));
    }

    [Fact]
    public void TarjetaValida_UsaCantidadDeDigitosYChecksum()
    {
        Assert.True(CompanyRegistrationValidation.IsValidCardNumber("4111111111111111"));
        Assert.False(CompanyRegistrationValidation.IsValidCardNumber("4111111111111112"));
        Assert.False(CompanyRegistrationValidation.IsValidCardNumber("411111111111"));
    }

    [Fact]
    public void CvcValido_ExigeExactamenteTresDigitos()
    {
        Assert.True(CompanyRegistrationValidation.IsValidCvc("123"));
        Assert.False(CompanyRegistrationValidation.IsValidCvc("12"));
        Assert.False(CompanyRegistrationValidation.IsValidCvc("1234"));
        Assert.False(CompanyRegistrationValidation.IsValidCvc("12a"));
    }

    [Fact]
    public void ExpiracionValida_AceptaSoloMesFuturo()
    {
        var currentMonth = new DateTime(2026, 9, 15);

        Assert.True(CompanyRegistrationValidation.IsValidExpiration("2026-10", currentMonth));
        Assert.False(CompanyRegistrationValidation.IsValidExpiration("2026-09", currentMonth));
        Assert.False(CompanyRegistrationValidation.IsValidExpiration("2025-12", currentMonth));
        Assert.False(CompanyRegistrationValidation.IsValidExpiration("09/26", currentMonth));
    }

    [Fact]
    public void ExpiracionSeleccionada_SeConvierteAlFormatoExistente()
    {
        Assert.Equal("10/26", CompanyRegistrationValidation.ToStoredExpiration("2026-10"));
    }
}
