using Gym.Domain.Common;

namespace Gym.Domain.Tests.Common;

/// <summary>The Jalali dates the SMS runs read (BUSINESS_RULES.md §10, the birthday by the Jalali day).</summary>
public sealed class JalaliCalendarTests
{
    [Theory]
    [InlineData(2026, 10, 6, 1405, 7, 14)]
    [InlineData(2026, 3, 21, 1405, 1, 1)]
    [InlineData(2026, 3, 20, 1404, 12, 29)]
    [InlineData(2025, 3, 20, 1403, 12, 30)]
    public void Parts_KnownDay_ReturnsItsJalaliDate(int year, int month, int day, int jalaliYear, int jalaliMonth, int jalaliDay)
    {
        JalaliCalendar.Parts(new DateOnly(year, month, day)).ShouldBe((jalaliYear, jalaliMonth, jalaliDay));
    }

    [Fact]
    public void ToDate_JalaliDay_ReturnsTheGregorianDay()
    {
        JalaliCalendar.ToDate(1405, 7, 14).ShouldBe(new DateOnly(2026, 10, 6));
    }

    [Theory]
    [InlineData(1403, true)]
    [InlineData(1404, false)]
    [InlineData(1405, false)]
    public void IsLeapYear_KnownYear_SaysWhetherEsfandHas30Days(int year, bool leap)
    {
        JalaliCalendar.IsLeapYear(year).ShouldBe(leap);
    }

    [Fact]
    public void BirthdayIn_OrdinaryDay_IsTheSameMonthAndDay()
    {
        var born = JalaliCalendar.ToDate(1370, 7, 20);

        JalaliCalendar.BirthdayIn(born, 1405).ShouldBe(JalaliCalendar.ToDate(1405, 7, 20));
    }

    [Fact]
    public void BirthdayIn_BornOn30EsfandInAYearWithout_Is29Esfand()
    {
        var born = JalaliCalendar.ToDate(1403, 12, 30);

        JalaliCalendar.BirthdayIn(born, 1404).ShouldBe(JalaliCalendar.ToDate(1404, 12, 29));
    }

    [Fact]
    public void BirthdayIn_BornOn30EsfandInALeapYear_Is30Esfand()
    {
        var born = JalaliCalendar.ToDate(1379, 12, 30);

        JalaliCalendar.BirthdayIn(born, 1403).ShouldBe(JalaliCalendar.ToDate(1403, 12, 30));
    }

    [Fact]
    public void NextBirthday_Today_IsToday()
    {
        var today = JalaliCalendar.ToDate(1405, 7, 14);

        JalaliCalendar.NextBirthday(JalaliCalendar.ToDate(1370, 7, 14), today).ShouldBe(today);
    }

    [Fact]
    public void NextBirthday_Yesterday_IsNextYear()
    {
        var today = JalaliCalendar.ToDate(1405, 7, 14);

        JalaliCalendar.NextBirthday(JalaliCalendar.ToDate(1370, 7, 13), today).ShouldBe(JalaliCalendar.ToDate(1406, 7, 13));
    }

    [Fact]
    public void NextBirthday_FarvardinSeenFromEsfand_IsInTheNewYear()
    {
        var today = JalaliCalendar.ToDate(1404, 12, 28);

        JalaliCalendar.NextBirthday(JalaliCalendar.ToDate(1370, 1, 1), today).ShouldBe(JalaliCalendar.ToDate(1405, 1, 1));
    }
}
