using Gym.Application.Accounts.RenameUser;
using Gym.Application.Accounts.SetPassword;
using Gym.Application.Accounts.UnlockUser;
using Gym.Domain.Common;

namespace Gym.Api.Admin;

/// <summary>
/// The account commands run on the server, not over HTTP: <c>dotnet Gym.Api.dll admin …</c>,
/// which <c>deploy/server.sh</c> wraps as <c>./server.sh unlock | set-password | rename</c>
/// (BUSINESS_RULES.md §1 *Lockout*).
/// </summary>
/// <remarks>
/// <para>
/// There is no endpoint for any of this on purpose. The web app cannot unlock or reset the Owner,
/// so the only way back in for a locked-out or forgotten Owner is from the server, and reaching
/// the server takes an SSH key. An endpoint would put that power one stolen password away.
/// </para>
/// <para>
/// The same host, configuration and database as the API, built but never started: no web server,
/// no background jobs, no seeding. The new password is read from standard input, never from the
/// command line, where it would sit in the shell history and in the process list.
/// </para>
/// </remarks>
public static partial class AdminConsole
{
    public const string Command = "admin";

    public const string Usage =
        "usage: admin unlock <user> | admin set-password <user> (password on standard input) | admin rename <user> <new-user>";

    public static bool IsAdminCommand(string[] args) => args is [Command, ..];

    /// <summary>Runs one command and returns the process exit code: 0 done, 1 refused, 2 bad usage.</summary>
    /// <param name="args">The arguments after <c>admin</c>.</param>
    public static async Task<int> RunAsync(
        IServiceProvider services,
        string[] args,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminConsole).FullName!);

        Result result;
        string done;

        switch (args)
        {
            case ["unlock", var userName]:
                result = await provider.GetRequiredService<UnlockUserHandler>().Handle(userName, cancellationToken);
                done = $"Unlocked {userName}.";
                break;

            case ["set-password", var userName]:
                var password = await input.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(password))
                {
                    await output.WriteLineAsync("error: no password on standard input.");
                    return 2;
                }

                result = await provider.GetRequiredService<SetPasswordHandler>().Handle(userName, password, cancellationToken);
                done = $"Password set for {userName}. Every session and trusted device of theirs has been signed out.";
                break;

            case ["rename", var userName, var newUserName]:
                result = await provider.GetRequiredService<RenameUserHandler>().Handle(userName, newUserName, cancellationToken);
                done = $"Renamed {userName} to {newUserName.Trim()}. They log in with the new name from now on.";
                break;

            default:
                await output.WriteLineAsync(Usage);
                return 2;
        }

        if (result.IsFailure)
        {
            await output.WriteLineAsync($"error: {result.Error.Code}: {result.Error.Description}");
            return 1;
        }

        // In the application log too, so there is a record of who was unlocked or renamed and when,
        // even though no web user did it.
        LogCommandDone(logger, args[0], args[1]);
        await output.WriteLineAsync(done);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server console: {Command} for user {UserName}.")]
    private static partial void LogCommandDone(ILogger logger, string command, string userName);
}
