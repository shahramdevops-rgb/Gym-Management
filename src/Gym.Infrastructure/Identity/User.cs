using Microsoft.AspNetCore.Identity;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// The one account type for both Owner and Staff; role membership (see <see cref="Roles"/>)
/// is what tells them apart, not the type.
/// </summary>
/// <remarks>
/// This is Identity's model, not a <c>Gym.Domain.Common.Entity</c>: <see cref="IdentityUser{TKey}"/>
/// already exposes its own properties with public setters for <c>UserManager</c> to write
/// through, so the private-setters rule that Domain entities follow does not fit here.
/// </remarks>
public sealed class User : IdentityUser<Guid>
{
    private User()
    {
    }

    public User(string userName, string fullName)
        : this()
    {
        // Matches Entity.Id's convention: a version 7 GUID sorts in creation order, and
        // setting it here (rather than leaving the base class's default) means EF Core's
        // Guid value generator sees a non-default value and uses it as-is instead of
        // generating its own on insert.
        Id = Guid.CreateVersion7();
        UserName = userName;
        FullName = fullName;
        IsActive = true;
        MustChangePassword = true;
    }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Deactivated users cannot log in; see login handling in a later task.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// True for every seeded or newly created account. A user with this set may only call
    /// change-password and logout (enforced in a later task, not here).
    /// </summary>
    public bool MustChangePassword { get; private set; }
}
