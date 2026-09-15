using System.Reflection;
using ApiCompania = CrownBuilders.API.Models.Compania;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class OptionalProfilePhotoTests
{
    [Fact]
    public void CompanyProfilePhoto_IsOptionalInApiModel()
    {
        var property = typeof(ApiCompania).GetProperty(nameof(ApiCompania.FotoPerfil));
        var nullability = new NullabilityInfoContext().Create(property!);

        Assert.Equal(NullabilityState.Nullable, nullability.ReadState);
    }
}
