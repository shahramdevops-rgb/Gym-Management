using Gym.Domain.Lockers;

namespace Gym.Domain.Tests.Lockers;

public sealed class LockerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(Locker.Count + 1)]
    public void Create_NumberOutsideTheGymsLockers_Throws(int number)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Locker.Create(number));
    }

    [Fact]
    public void MarkOutOfService_NotOccupied_SucceedsAndSetsTheFlag()
    {
        var locker = Locker.Create(1);

        var result = locker.MarkOutOfService(isOccupied: false);

        result.IsSuccess.ShouldBeTrue();
        locker.IsOutOfService.ShouldBeTrue();
    }

    [Fact]
    public void MarkOutOfService_Occupied_FailsWithOccupiedAndChangesNothing()
    {
        var locker = Locker.Create(1);

        var result = locker.MarkOutOfService(isOccupied: true);

        result.Error.ShouldBe(LockerErrors.Occupied);
        locker.IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public void MarkOutOfService_AlreadyOutOfService_SucceedsAndStaysOutOfService()
    {
        var locker = Locker.Create(1);
        locker.MarkOutOfService(isOccupied: false);

        var result = locker.MarkOutOfService(isOccupied: false);

        result.IsSuccess.ShouldBeTrue();
        locker.IsOutOfService.ShouldBeTrue();
    }

    [Fact]
    public void MarkInService_OutOfServiceLocker_ClearsTheFlag()
    {
        var locker = Locker.Create(1);
        locker.MarkOutOfService(isOccupied: false);

        locker.MarkInService();

        locker.IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public void MarkInService_AlreadyInService_StaysInService()
    {
        var locker = Locker.Create(1);

        locker.MarkInService();

        locker.IsOutOfService.ShouldBeFalse();
    }
}
