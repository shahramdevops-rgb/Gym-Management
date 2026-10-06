using Gym.Application.Common.Sms;
using Gym.Domain.Notifications;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The test host's <see cref="ISmsRunSchedule"/>: it remembers what it was asked and schedules nothing.
/// </summary>
/// <remarks>
/// The test host runs a real Hangfire server against the test database. A run scheduled by a test that
/// saves the settings page would fire at its real time, in the middle of some later test, and write
/// messages nobody expects. The real schedule is tested on its own in <c>SmsRunScheduleTests</c>.
/// </remarks>
internal sealed class RecordingSmsRunSchedule : ISmsRunSchedule
{
    private readonly List<SmsSettings> _applied = [];

    /// <summary>The settings it was given, oldest first. The database collection runs one test at a time.</summary>
    public IReadOnlyList<SmsSettings> Applied => _applied;

    public void Apply(SmsSettings settings) => _applied.Add(settings);

    public void Clear() => _applied.Clear();
}
