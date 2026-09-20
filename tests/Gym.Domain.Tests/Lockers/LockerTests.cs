using Gym.Domain.Lockers;

namespace Gym.Domain.Tests.Lockers;

public sealed class LockerTests
{
    [Fact]
    public void Create_ValidNumber_IsInServiceWithTheGivenNumber()
    {
        var locker = Locker.Create(7).Value;

        locker.Number.ShouldBe(7);
        locker.IsOutOfService.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveNumber_FailsWithNumberInvalid(int number)
    {
        Locker.Create(number).Error.ShouldBe(LockerErrors.NumberInvalid);
    }

    [Fact]
    public void Create_MinimumNumber_Succeeds()
    {
        Locker.Create(Locker.MinNumber).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void MarkOutOfService_NotOccupied_SucceedsAndSetsTheFlag()
    {
        var locker = Locker.Create(1).Value;

        var result = locker.MarkOutOfService(isOccupied: false);

        result.IsSuccess.ShouldBeTrue();
        locker.IsOutOfService.ShouldBeTrue();
    }

    [Fact]
    public void MarkOutOfService_Occupied_FailsWithOccupiedAndChangesNothing()
    {
        var locker = Locker.Create(1).Value;

        var result = locker.MarkOutOfService(isOccupied: true);

        result.Error.ShouldBe(LockerErrors.Occupied);
        locker.IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public void MarkOutOfService_AlreadyOutOfService_SucceedsAndStaysOutOfService()
    {
        var locker = Locker.Create(1).Value;
        locker.MarkOutOfService(isOccupied: false);

        var result = locker.MarkOutOfService(isOccupied: false);

        result.IsSuccess.ShouldBeTrue();
        locker.IsOutOfService.ShouldBeTrue();
    }

    [Fact]
    public void MarkInService_OutOfServiceLocker_ClearsTheFlag()
    {
        var locker = Locker.Create(1).Value;
        locker.MarkOutOfService(isOccupied: false);

        locker.MarkInService();

        locker.IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public void MarkInService_AlreadyInService_StaysInService()
    {
        var locker = Locker.Create(1).Value;

        locker.MarkInService();

        locker.IsOutOfService.ShouldBeFalse();
    }
}
