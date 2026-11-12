using System.Security.Claims;

using Gym.Api.Authorization;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Auth;

/// <summary>
/// Each policy evaluated by the running app's own <see cref="IAuthorizationService"/> against
/// hand-built users, so every role and flag combination is covered without an endpoint for each.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AuthorizationPolicyTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly ClaimsPrincipal Owner = User(Roles.Owner, mustChangePassword: false);
    private static readonly ClaimsPrincipal Staff = User(Roles.Staff, mustChangePassword: false);
    private static readonly ClaimsPrincipal OwnerWithTemporaryPassword = User(Roles.Owner, mustChangePassword: true);
    private static readonly ClaimsPrincipal StaffWithTemporaryPassword = User(Roles.Staff, mustChangePassword: true);
    private static readonly ClaimsPrincipal ShopAdmin = User(Roles.Shop, mustChangePassword: false);
    private static readonly ClaimsPrincipal ShopAdminWithTemporaryPassword = User(Roles.Shop, mustChangePassword: true);
    private static readonly ClaimsPrincipal NoRole = User(role: null, mustChangePassword: false);
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public static TheoryData<string, string, bool> Cases => new()
    {
        { Policies.OwnerOnly, nameof(Owner), true },
        { Policies.OwnerOnly, nameof(Staff), false },
        { Policies.OwnerOnly, nameof(OwnerWithTemporaryPassword), false },
        { Policies.OwnerOnly, nameof(ShopAdmin), false },
        { Policies.OwnerOnly, nameof(NoRole), false },
        { Policies.OwnerOnly, nameof(Anonymous), false },

        { Policies.StaffOrOwner, nameof(Owner), true },
        { Policies.StaffOrOwner, nameof(Staff), true },
        { Policies.StaffOrOwner, nameof(StaffWithTemporaryPassword), false },
        { Policies.StaffOrOwner, nameof(ShopAdmin), false },
        { Policies.StaffOrOwner, nameof(NoRole), false },
        { Policies.StaffOrOwner, nameof(Anonymous), false },

        // The shop (ADR 0011, §16): the shop admin changes it, the Owner reads it, Staff never.
        { Policies.ShopAdmin, nameof(ShopAdmin), true },
        { Policies.ShopAdmin, nameof(ShopAdminWithTemporaryPassword), false },
        { Policies.ShopAdmin, nameof(Owner), false },
        { Policies.ShopAdmin, nameof(Staff), false },
        { Policies.ShopAdmin, nameof(NoRole), false },
        { Policies.ShopAdmin, nameof(Anonymous), false },

        { Policies.ShopOrOwner, nameof(ShopAdmin), true },
        { Policies.ShopOrOwner, nameof(Owner), true },
        { Policies.ShopOrOwner, nameof(Staff), false },
        { Policies.ShopOrOwner, nameof(ShopAdminWithTemporaryPassword), false },
        { Policies.ShopOrOwner, nameof(NoRole), false },

        { Policies.OwnerStaffOrShop, nameof(Owner), true },
        { Policies.OwnerStaffOrShop, nameof(Staff), true },
        { Policies.OwnerStaffOrShop, nameof(ShopAdmin), true },
        { Policies.OwnerStaffOrShop, nameof(ShopAdminWithTemporaryPassword), false },
        { Policies.OwnerStaffOrShop, nameof(NoRole), false },
        { Policies.OwnerStaffOrShop, nameof(Anonymous), false },

        { Policies.PasswordChangeAllowed, nameof(OwnerWithTemporaryPassword), true },
        { Policies.PasswordChangeAllowed, nameof(StaffWithTemporaryPassword), true },
        { Policies.PasswordChangeAllowed, nameof(Staff), true },
        { Policies.PasswordChangeAllowed, nameof(ShopAdminWithTemporaryPassword), true },
        { Policies.PasswordChangeAllowed, nameof(Anonymous), false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Policy_ForUser_AllowsOnlyWhatBusinessRulesPermit(string policy, string userName, bool expected)
    {
        var result = await AuthorizeAsync(Principal(userName), policy);

        result.Succeeded.ShouldBe(expected);
    }

    [Fact]
    public async Task OwnerOnly_OwnerWithTemporaryPassword_FailsOnTheGateRequirement()
    {
        var result = await AuthorizeAsync(OwnerWithTemporaryPassword, Policies.OwnerOnly);

        // This is what ProblemDetailsAuthorizationResultHandler reads to answer
        // Auth.PasswordChangeRequired instead of Auth.Forbidden.
        result.Failure.ShouldNotBeNull().FailedRequirements.OfType<PasswordChangedRequirement>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FallbackPolicy_UserWithTemporaryPassword_IsRefused()
    {
        await using var scope = Fixture.CreateScope();
        var fallback = await scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        // An endpoint that forgot its policy is still gated.
        (await authorization.AuthorizeAsync(StaffWithTemporaryPassword, fallback.ShouldNotBeNull())).Succeeded.ShouldBeFalse();
        (await authorization.AuthorizeAsync(Staff, fallback)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task FallbackPolicy_ShopAdmin_IsRefused()
    {
        await using var scope = Fixture.CreateScope();
        var fallback = await scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        // An endpoint that ever forgot its policy still refuses the shop admin (ADR 0011).
        (await authorization.AuthorizeAsync(ShopAdmin, fallback.ShouldNotBeNull())).Succeeded.ShouldBeFalse();
        (await authorization.AuthorizeAsync(Owner, fallback)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task StaffOrOwner_TokenWithoutTheGateClaim_IsRefused()
    {
        var identity = new ClaimsIdentity(
            [new Claim(JwtClaimNames.Subject, Guid.CreateVersion7().ToString()), new Claim(JwtClaimNames.Role, Roles.Staff)],
            authenticationType: "Test",
            nameType: JwtClaimNames.Name,
            roleType: JwtClaimNames.Role);

        // A missing claim is not "false": every token this API issues carries it.
        (await AuthorizeAsync(new ClaimsPrincipal(identity), Policies.StaffOrOwner)).Succeeded.ShouldBeFalse();
    }

    private async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policy)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);
    }

    private static ClaimsPrincipal Principal(string name) => name switch
    {
        nameof(Owner) => Owner,
        nameof(Staff) => Staff,
        nameof(OwnerWithTemporaryPassword) => OwnerWithTemporaryPassword,
        nameof(StaffWithTemporaryPassword) => StaffWithTemporaryPassword,
        nameof(ShopAdmin) => ShopAdmin,
        nameof(ShopAdminWithTemporaryPassword) => ShopAdminWithTemporaryPassword,
        nameof(NoRole) => NoRole,
        nameof(Anonymous) => Anonymous,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown test user."),
    };

    /// <summary>The same claims and claim types as a real access token.</summary>
    private static ClaimsPrincipal User(string? role, bool mustChangePassword)
    {
        var claims = new List<Claim>
        {
            new(JwtClaimNames.Subject, Guid.CreateVersion7().ToString()),
            new(JwtClaimNames.Name, "user"),
            new(JwtClaimNames.MustChangePassword, mustChangePassword ? "true" : "false"),
        };

        if (role is not null)
        {
            claims.Add(new Claim(JwtClaimNames.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Test",
            nameType: JwtClaimNames.Name,
            roleType: JwtClaimNames.Role));
    }
}
