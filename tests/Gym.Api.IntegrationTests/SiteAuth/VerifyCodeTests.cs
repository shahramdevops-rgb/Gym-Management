using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.SiteAuth.VerifyCode;
using Gym.Domain.Common;
using Gym.Domain.SiteAuth;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// Checking a typed code (BUSINESS_RULES.md §15): each wrong try is counted in the database, even
/// though the answer is a failure, and two guesses at once are both counted.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class VerifyCodeTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";

    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);

    [Fact]
    public async Task Handle_RightCodeInPersianDigits_ReturnsThePhoneAndUsesTheCodeUp()
    {
        var code = await IssueAsync();

        var result = await HandleAsync("09121234567", ToPersianDigits(code));

        result.Value.ShouldBe(Phone);
        (await RowAsync()).UsedAt.ShouldBe(Start);
        (await HandleAsync(Phone, code)).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    [Fact]
    public async Task Handle_WrongCode_CountsTheTryInTheDatabase()
    {
        var code = await IssueAsync();

        var result = await HandleAsync(Phone, Wrong(code));

        result.Error.ShouldBe(SignInCodeErrors.Wrong);
        (await RowAsync()).WrongTries.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_FifthWrongTry_EndsTheCodeForGood()
    {
        var code = await IssueAsync();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await HandleAsync(Phone, Wrong(code));
        }

        (await HandleAsync(Phone, Wrong(code))).Error.ShouldBe(SignInCodeErrors.TooManyWrongTries);
        (await HandleAsync(Phone, code)).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    [Fact]
    public async Task Handle_AfterSixtyMinutes_IsExpired()
    {
        var code = await IssueAsync();
        _time.Advance(SignInCode.Lifetime);

        (await HandleAsync(Phone, code)).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    [Fact]
    public async Task Handle_NoCodeEverSentToThePhone_IsExpired()
    {
        (await HandleAsync(Phone, "12345")).Error.ShouldBe(SignInCodeErrors.Expired);
    }

    [Fact]
    public async Task Handle_TwoWrongGuessesAtOnce_BothAreCounted()
    {
        var code = await IssueAsync();

        await Task.WhenAll(HandleAsync(Phone, Wrong(code)), HandleAsync(Phone, Wrong(code)));

        (await RowAsync()).WrongTries.ShouldBe(2);
    }

    // ---- Helpers ----

    /// <summary>A code for <see cref="Phone"/>, straight into the table, and what it is.</summary>
    private async Task<string> IssueAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = SignInCode.Issue(Phone, SignInCodeSeed.Generate(), Start);
        db.SignInCodes.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return scope.ServiceProvider.GetRequiredService<ISignInCodes>().CodeFor(Phone, row.CodeSeed);
    }

    private async Task<Result<string>> HandleAsync(string phone, string code)
    {
        await using var scope = Fixture.CreateScope();
        var services = scope.ServiceProvider;
        var handler = new VerifyCodeHandler(
            services.GetRequiredService<IAppDbContext>(),
            services.GetRequiredService<IPhoneNormalizer>(),
            services.GetRequiredService<ISignInCodes>(),
            _time);

        return await handler.Handle(new VerifyCodeCommand(phone, code), TestContext.Current.CancellationToken);
    }

    private async Task<SignInCode> RowAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SignInCodes
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The code with its last digit moved on by one: always wrong, always 5 digits.</summary>
    private static string Wrong(string code) => string.Concat(code.AsSpan(0, code.Length - 1), ((code[^1] - '0' + 1) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string ToPersianDigits(string digits) => new([.. digits.Select(digit => (char)('۰' + (digit - '0')))]);
}
