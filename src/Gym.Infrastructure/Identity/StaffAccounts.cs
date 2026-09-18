using Gym.Application.Common.Paging;
using Gym.Application.Staff;
using Gym.Domain.Common;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Gym.Infrastructure.Identity;

/// <summary>
/// <see cref="IStaffAccounts"/> on top of Identity's <see cref="UserManager{TUser}"/>, plus
/// <see cref="AppDbContext"/> for the paged list, which UserManager cannot do.
/// </summary>
/// <remarks>
/// UserManager and the context are the same scoped instance underneath, so the handler's
/// transaction covers every save made here.
/// </remarks>
public sealed class StaffAccounts(
    UserManager<User> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    AppDbContext db,
    TimeProvider timeProvider) : IStaffAccounts
{
    private const string UniqueViolation = "23505";

    public async Task<Result<StaffResponse>> CreateAsync(
        string userName,
        string fullName,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The seeder creates the roles only when Seed:* is configured, so do not rely on it.
        if (!await roleManager.RoleExistsAsync(Roles.Staff))
        {
            ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Staff)), "create the Staff role");
        }

        var user = new User(userName, fullName);

        IdentityResult created;
        try
        {
            created = await userManager.CreateAsync(user, temporaryPassword);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Identity checks for a duplicate first, but two requests can pass that check at
            // the same moment. The unique index decides, and the loser gets the same answer.
            return Result.Failure<StaffResponse>(StaffErrors.UserNameTaken);
        }

        if (!created.Succeeded)
        {
            return Result.Failure<StaffResponse>(
                created.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                    ? StaffErrors.UserNameTaken
                    : StaffErrors.Rejected(Describe(created)));
        }

        ThrowIfFailed(await userManager.AddToRoleAsync(user, Roles.Staff), "add the user to the Staff role");

        return ToResponse(user);
    }

    public async Task<PagedResponse<StaffResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var staff = StaffUsers();

        var totalCount = await staff.CountAsync(cancellationToken);
        var items = await staff
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new StaffResponse(
                user.Id,
                user.UserName!,
                user.FullName,
                user.IsActive,
                user.MustChangePassword,
                user.LockoutEnd != null && user.LockoutEnd > now))
            .ToListAsync(cancellationToken);

        return new PagedResponse<StaffResponse>(items, page, pageSize, totalCount);
    }

    public async Task<StaffResponse?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindStaffAsync(id, cancellationToken);

        return user is null ? null : ToResponse(user);
    }

    public async Task<Result<StaffResponse>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var user = await FindStaffAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure<StaffResponse>(StaffErrors.NotFound);
        }

        if (user.IsActive != isActive)
        {
            if (isActive)
            {
                user.Reactivate();
            }
            else
            {
                user.Deactivate();
            }

            ThrowIfFailed(await userManager.UpdateAsync(user), "update the account");
        }

        return ToResponse(user);
    }

    public async Task<Result> ResetPasswordAsync(Guid id, string temporaryPassword, CancellationToken cancellationToken)
    {
        var user = await FindStaffAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(StaffErrors.NotFound);
        }

        // Validated before anything changes, so a refused password leaves the old one in place
        // even if a caller forgot the transaction.
        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, user, temporaryPassword);
            if (!validation.Succeeded)
            {
                return Result.Failure(StaffErrors.Rejected(Describe(validation)));
            }
        }

        ThrowIfFailed(await userManager.RemovePasswordAsync(user), "remove the old password");
        ThrowIfFailed(await userManager.AddPasswordAsync(user, temporaryPassword), "set the temporary password");

        // The reset is how a locked-out staff member gets back in, so it clears the lockout.
        ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user, null), "clear the lockout");
        ThrowIfFailed(await userManager.ResetAccessFailedCountAsync(user), "reset the failed login count");

        user.RequirePasswordChange();
        ThrowIfFailed(await userManager.UpdateAsync(user), "require a password change");

        return Result.Success();
    }

    /// <summary>Users in the Staff role. The Owner and any other role are invisible here.</summary>
    private IQueryable<User> StaffUsers() =>
        from user in db.Users
        where db.UserRoles.Any(link => link.UserId == user.Id &&
            db.Roles.Any(role => role.Id == link.RoleId && role.Name == Roles.Staff))
        select user;

    private Task<User?> FindStaffAsync(Guid id, CancellationToken cancellationToken) =>
        StaffUsers().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    private StaffResponse ToResponse(User user) => new(
        user.Id,
        user.UserName!,
        user.FullName,
        user.IsActive,
        user.MustChangePassword,
        user.LockoutEnd is { } lockoutEnd && lockoutEnd > timeProvider.GetUtcNow());

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {action}: {Describe(result)}");
        }
    }
}
