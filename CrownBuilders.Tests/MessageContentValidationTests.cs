using CrownBuilders.API.Services;
using Xunit;

namespace CrownBuilders.Tests;

public class MessageContentValidationTests
{
    [Fact]
    public void Validate_RejectsTextLongerThan4000Characters()
    {
        var result = MessageContentValidator.Validate(new string('a', MessageContentValidator.MaxLength + 1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsBlankText()
    {
        var result = MessageContentValidator.Validate("  ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_AcceptsTextAtMaximumLength()
    {
        var result = MessageContentValidator.Validate(new string('a', MessageContentValidator.MaxLength));

        Assert.True(result.IsValid);
    }
}
