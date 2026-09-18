namespace Gym.Domain.Auth;

/// <summary>Why a refresh token stopped working. Stored as text, so the database reads plainly.</summary>
public enum RefreshTokenRevocationReason
{
    /// <summary>Used once and replaced by its child. The normal end of every token's life.</summary>
    Rotated = 1,

    /// <summary>A rotated token came back, so the whole family was revoked.</summary>
    ReuseDetected = 2,

    /// <summary>The user logged out.</summary>
    Logout = 3,

    /// <summary>The user was deactivated.</summary>
    UserInactive = 4,

    /// <summary>The user changed their password, which logs out every other browser.</summary>
    PasswordChanged = 5,

    /// <summary>The Owner reset the user's password.</summary>
    PasswordReset = 6,
}
