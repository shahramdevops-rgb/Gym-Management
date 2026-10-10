namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>Reads a file of the repository itself, for tests that check code against config or the frontend.</summary>
internal static class RepositoryFiles
{
    /// <summary>The text of a file, by its path from the repository root (for example <c>deploy/Caddyfile</c>).</summary>
    public static string ReadAllText(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GymManagement.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"Could not find GymManagement.sln in any directory above '{AppContext.BaseDirectory}'.");
    }
}
