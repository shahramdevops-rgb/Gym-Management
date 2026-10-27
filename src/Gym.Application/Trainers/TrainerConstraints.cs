namespace Gym.Application.Trainers;

/// <summary>
/// Database index names the trainer handlers react to, named once here and used by the
/// configurations that create them, as <c>MemberConstraints</c> does.
/// </summary>
public static class TrainerConstraints
{
    /// <summary>Phones are unique among trainers, stopped ones included (BUSINESS_RULES.md §15).</summary>
    public const string UniquePhone = "ix_trainers_phone_number";

    /// <summary>A trainer has one open period at most: what refuses two «شروع دوباره» at once.</summary>
    public const string OneOpenPeriod = "ix_trainer_periods_trainer_id_open";
}
