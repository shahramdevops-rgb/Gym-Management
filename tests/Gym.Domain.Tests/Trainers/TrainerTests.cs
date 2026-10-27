using Gym.Domain.Trainers;

namespace Gym.Domain.Tests.Trainers;

public sealed class TrainerTests
{
    private const string Phone = "+989121234567";

    /// <summary>The gym's today, named rather than read from a clock.</summary>
    private static readonly DateOnly Today = new(2026, 10, 27);

    [Fact]
    public void Create_ValidDetails_IsATrainerFromToday()
    {
        var trainer = Trainer.Create("  علی کریمی ", Phone, Today).Value;

        trainer.FullName.ShouldBe("علی کریمی");
        trainer.PhoneNumber.ShouldBe(Phone);
        trainer.IsActive.ShouldBeTrue();
        var period = trainer.Periods.ShouldHaveSingleItem();
        period.TrainerId.ShouldBe(trainer.Id);
        period.StartedOn.ShouldBe(Today);
        period.EndedOn.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_FailsWithFullNameRequired(string fullName)
    {
        Trainer.Create(fullName, Phone, Today).Error.ShouldBe(TrainerErrors.FullNameRequired);
    }

    [Fact]
    public void Create_NameTooLong_FailsWithFullNameTooLong()
    {
        Trainer.Create(new string('ع', Trainer.FullNameMaxLength + 1), Phone, Today).Error.ShouldBe(TrainerErrors.FullNameTooLong);
    }

    [Fact]
    public void Create_PhoneNotNormalized_Throws()
    {
        // A caller that skipped IPhoneNormalizer is a bug, as for a member.
        Should.Throw<ArgumentException>(() => Trainer.Create("علی کریمی", "09121234567", Today));
    }

    [Fact]
    public void Update_NewNameAndPhone_ReplacesBoth()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;

        trainer.Update("علی کریمی‌نژاد", "+989351234567").IsSuccess.ShouldBeTrue();

        trainer.FullName.ShouldBe("علی کریمی‌نژاد");
        trainer.PhoneNumber.ShouldBe("+989351234567");
    }

    [Fact]
    public void Update_WhileStopped_Succeeds()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;
        trainer.Stop(Today.AddDays(3));

        trainer.Update("علی کریمی", "+989351234567").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_BlankName_FailsAndChangesNothing()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;

        trainer.Update(" ", "+989351234567").Error.ShouldBe(TrainerErrors.FullNameRequired);

        trainer.FullName.ShouldBe("علی کریمی");
        trainer.PhoneNumber.ShouldBe(Phone);
    }

    [Fact]
    public void Stop_ActiveTrainer_EndsTheOpenPeriodToday()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;

        trainer.Stop(Today.AddDays(10)).IsSuccess.ShouldBeTrue();

        trainer.IsActive.ShouldBeFalse();
        trainer.Periods.ShouldHaveSingleItem().EndedOn.ShouldBe(Today.AddDays(10));
    }

    [Fact]
    public void Stop_OnTheDayTheyStarted_EndsThatDay()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;

        trainer.Stop(Today).IsSuccess.ShouldBeTrue();

        trainer.Periods.ShouldHaveSingleItem().EndedOn.ShouldBe(Today);
    }

    [Fact]
    public void Stop_AlreadyStopped_FailsWithAlreadyStopped()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;
        trainer.Stop(Today.AddDays(1));

        trainer.Stop(Today.AddDays(2)).Error.ShouldBe(TrainerErrors.AlreadyStopped);

        trainer.Periods.ShouldHaveSingleItem().EndedOn.ShouldBe(Today.AddDays(1), "the first end stays.");
    }

    [Fact]
    public void Restart_StoppedTrainer_OpensANewPeriodAndKeepsTheOldOne()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;
        trainer.Stop(Today.AddDays(30));

        trainer.Restart(Today.AddDays(60)).IsSuccess.ShouldBeTrue();

        trainer.IsActive.ShouldBeTrue();
        trainer.Periods.Count.ShouldBe(2);
        trainer.Periods[0].StartedOn.ShouldBe(Today);
        trainer.Periods[0].EndedOn.ShouldBe(Today.AddDays(30));
        trainer.Periods[1].StartedOn.ShouldBe(Today.AddDays(60));
        trainer.Periods[1].EndedOn.ShouldBeNull();
    }

    [Fact]
    public void Restart_ActiveTrainer_FailsWithAlreadyActive()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;

        trainer.Restart(Today.AddDays(1)).Error.ShouldBe(TrainerErrors.AlreadyActive);

        trainer.Periods.ShouldHaveSingleItem();
    }

    [Fact]
    public void Stop_AfterARestart_EndsOnlyTheNewPeriod()
    {
        var trainer = Trainer.Create("علی کریمی", Phone, Today).Value;
        trainer.Stop(Today.AddDays(30));
        trainer.Restart(Today.AddDays(60));

        trainer.Stop(Today.AddDays(90)).IsSuccess.ShouldBeTrue();

        trainer.Periods[0].EndedOn.ShouldBe(Today.AddDays(30));
        trainer.Periods[1].EndedOn.ShouldBe(Today.AddDays(90));
    }
}
