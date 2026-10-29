using Gym.Infrastructure.Identity;
using Gym.Infrastructure.SiteAuth;

using Microsoft.Extensions.Options;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// The startup check of ADR 0010: the site's key, issuer and audience must exist, and must never be
/// the staff's, or one kind of token could pass for the other.
/// </summary>
public sealed class SiteAuthOptionsTests
{
    private static readonly JwtOptions Staff = new()
    {
        Issuer = "gym-api",
        Audience = "gym-web",
        SigningKey = "staff-signing-key-0123456789abcdef0123",
    };

    [Fact]
    public void Validate_OwnSecretsIssuerAndAudience_Succeeds()
    {
        Validate(Valid()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_TheStaffsSigningKey_Fails()
    {
        var options = Valid();
        options.SigningKey = Staff.SigningKey;

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull().ShouldContain("SiteAuth:SigningKey must differ from Jwt:SigningKey");
    }

    [Fact]
    public void Validate_TheStaffsIssuer_Fails()
    {
        var options = Valid();
        options.Issuer = Staff.Issuer;

        Validate(options).FailureMessage.ShouldNotBeNull().ShouldContain("SiteAuth:Issuer");
    }

    [Fact]
    public void Validate_TheStaffsAudience_Fails()
    {
        var options = Valid();
        options.Audience = Staff.Audience;

        Validate(options).FailureMessage.ShouldNotBeNull().ShouldContain("SiteAuth:Audience");
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void Validate_ShortSecrets_FailNamingBoth(string secret)
    {
        var options = Valid();
        options.SigningKey = secret;
        options.CodeSecret = secret;

        var result = Validate(options);

        result.FailureMessage.ShouldNotBeNull().ShouldContain("SiteAuth:SigningKey");
        result.FailureMessage.ShouldNotBeNull().ShouldContain("SiteAuth:CodeSecret");
    }

    private static SiteAuthOptions Valid() => new()
    {
        Issuer = "gym-site",
        Audience = "gym-site-app",
        SigningKey = "site-signing-key-0123456789abcdef01234",
        CodeSecret = "site-code-secret-0123456789abcdef01234",
    };

    private static ValidateOptionsResult Validate(SiteAuthOptions options) =>
        new SiteAuthOptionsValidator(Options.Create(Staff)).Validate(null, options);
}
