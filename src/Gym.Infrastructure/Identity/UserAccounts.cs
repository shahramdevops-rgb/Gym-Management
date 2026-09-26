using Gym.Application.Accounts;
using Gym.Domain.Common;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Gym.Infrastructure.Identity;

/// <summary><see cref="IUserAccounts"/> on top of Identity's <see cref="UserManager{TUser}"/>.</summary>
/// <remarks>
/// The same scoped <see cref="AppDbContext"/> as UserManager's, so the handler's transaction
/// covers every save made here, as in <see cref="StaffAccounts"/>.
/// </remarks>
public sealed class UserAccounts(UserManager<User> userManager, AppDbContext db) : IUserAccounts
{
    private const string UniqueViolation = "23505";

    public async Task<Guid?> FindIdByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (await userManager.FindByNameAsync(userName.Trim()))?.Id;
    }

    public async Task<Result> UnlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindExistingAsync(userId);

        return ToResult(await Lockouts.ClearAsync(userManager, db, user, cancellationToken));
    }

    public async Task<Result> SetPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        var user = await FindExistingAsync(userId);

        // Checked before anything changes, so a refused password leaves the old one in place.
        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, user, password);
            if (!validation.Succeeded)
            {
                return Result.Failure(PasswordPolicyValidator.PolicyError(validation) ?? AccountErrors.Rejected(Describe(validation)));
            }
        }

        // They typed it themselves, so there is nothing temporary to replace.
        user.PasswordChanged();

        var steps = new Func<Task<IdentityResult>>[]
        {
            () => userManager.RemovePasswordAsync(user),
            () => userManager.AddPasswordAsync(user, password),
            () => Lockouts.ClearAsync(userManager, db, user, cancellationToken),
        };

        foreach (var step in steps)
        {
            var result = ToResult(await step());
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    public async Task<Result> RenameAsync(Guid userId, string newUserName, CancellationToken cancellationToken)
    {
        var user = await FindExistingAsync(userId);

        IdentityResult renamed;
        try
        {
            renamed = await userManager.SetUserNameAsync(user, newUserName);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            return Result.Failure(AccountErrors.UserNameTaken);
        }

        if (renamed.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Result.Failure(AccountErrors.UserNameTaken);
        }

        return ToResult(renamed);
    }

    /// <summary>Every caller found this id by user name a moment ago; users are never deleted.</summary>
    private async Task<User> FindExistingAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new InvalidOperationException($"User {userId} does not exist.");

    private static Result ToResult(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return Result.Success();
        }

        return Result.Failure(
            result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
                ? AccountErrors.ChangedConcurrently
                : AccountErrors.Rejected(Describe(result)));
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));
}
