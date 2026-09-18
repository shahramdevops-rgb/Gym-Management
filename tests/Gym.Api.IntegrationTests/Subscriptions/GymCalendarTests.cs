using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Infrastructure.Calendar;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>"Today" in the gym's time zone (BUSINESS_RULES.md §0). No database needed.</summary>
public sealed class GymCalendarTests
{
    [Theory]
    [InlineData(20, 29, 30)] // 23:59 in Tehran: still the 30th
    [InlineData(20, 30, 1)]  // 00:00 in Tehran: already the 1st of October
    public void Today_AroundTehranMidnight_UsesTheGymsDate(int utcHour, int utcMinute, int expectedDay)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, utcHour, utcMinute, 0, TimeSpan.Zero));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        calendar.Today().Day.ShouldBe(expectedDay);
    }

    [Theory]
    [InlineData("Asia/Tehran", true)]
    [InlineData("Mars/Olympus_Mons", false)]
    [InlineData("", false)]
    public void Validate_TimeZone_AcceptsOnlyKnownZones(string timeZone, bool valid)
    {
        var result = new GymCalendarOptionsValidator().Validate(null, new GymCalendarOptions { TimeZone = timeZone });

        result.Succeeded.ShouldBe(valid);
    }
}

/// <summary>The app refuses to start with a time zone it does not know, instead of guessing a day.</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class GymCalendarStartupTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public void Startup_UnknownTimeZone_Fails()
    {
        var settings = new Dictionary<string, string?> { ["Gym:TimeZone"] = "Mars/Olympus_Mons" };

        Should.Throw<OptionsValidationException>(() => Fixture.CreateClient(settings));
    }
}
