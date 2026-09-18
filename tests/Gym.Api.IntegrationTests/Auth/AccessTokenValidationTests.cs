using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Security;
using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// Tokens checked with the running app's own JWT bearer settings, so these tests prove what
/// the API would actually accept rather than what a copy of its settings would.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AccessTokenValidationTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly AuthenticatedUser Owner =
        new(Guid.CreateVersion7(), "owner", "مدیر", [Roles.Owner], MustChangePassword: false);

    [Fact]
    public async Task AccessToken_JustIssued_IsAcceptedByBearerValidation()
    {
        var token = IssueAt(TimeProvider.System.GetUtcNow());

        var result = await ValidateAsync(token);

        result.IsValid.ShouldBeTrue();
        result.ClaimsIdentity.IsAuthenticated.ShouldBeTrue();
        result.ClaimsIdentity.Name.ShouldBe("owner");
    }

    [Fact]
    public async Task AccessToken_OlderThanFifteenMinutes_IsRejectedAsExpired()
    {
        // Issued 15 minutes and 1 second ago. With the default 5-minute clock skew this would
        // still pass; ClockSkew = 0 is what makes 15 minutes mean 15 minutes.
        var token = IssueAt(TimeProvider.System.GetUtcNow().AddMinutes(-15).AddSeconds(-1));

        var result = await ValidateAsync(token);

        result.IsValid.ShouldBeFalse();
        result.Exception.ShouldBeOfType<SecurityTokenExpiredException>();
    }

    [Fact]
    public async Task AccessToken_SignedWithAnotherKey_IsRejected()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = AppJwtOptions().Issuer,
            Audience = AppJwtOptions().Audience,
            SigningKey = "a-different-key-of-at-least-thirty-two-bytes",
        });
        var token = new JwtAccessTokenIssuer(options, TimeProvider.System).Issue(Owner).Value;

        var result = await ValidateAsync(token);

        result.IsValid.ShouldBeFalse();
    }

    private string IssueAt(DateTimeOffset issuedAt)
    {
        var issuer = new JwtAccessTokenIssuer(Options.Create(AppJwtOptions()), new FakeTimeProvider(issuedAt));

        return issuer.Issue(Owner).Value;
    }

    private JwtOptions AppJwtOptions()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
    }

    private async Task<TokenValidationResult> ValidateAsync(string token)
    {
        await using var scope = Fixture.CreateScope();
        var bearer = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        return await new JsonWebTokenHandler().ValidateTokenAsync(token, bearer.TokenValidationParameters);
    }
}
