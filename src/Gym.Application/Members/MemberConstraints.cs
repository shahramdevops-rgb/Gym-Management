namespace Gym.Application.Members;

/// <summary>
/// Database index names the member handlers react to. Named once here, because a handler that
/// catches a unique violation must know which index broke, and the configuration that creates
/// the index uses the same constant.
/// </summary>
public static class MemberConstraints
{
    public const string UniquePhone = "ix_members_phone_number";
}
