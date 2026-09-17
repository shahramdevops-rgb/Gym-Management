using System.Text.Json;

namespace Gym.Api.IntegrationTests.Configuration;

/// <summary>
/// Holds the two halves of the local database configuration against each other.
/// appsettings.Development.json is committed, so it must never carry the password, and the
/// rest of its connection string must keep matching the container that .env.example
/// describes. These read the repository's own files rather than the build output, because
/// what matters is what gets committed.
/// </summary>
public sealed class DevelopmentSettingsTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly Dictionary<string, string> ConnectionString =
        ParseConnectionString(ReadDevelopmentConnectionString());

    private static readonly Dictionary<string, string> EnvironmentTemplate =
        ParseEnvironmentFile(ReadRepositoryFile(".env.example"));

    [Theory]
    [InlineData("Password")]
    [InlineData("Pwd")]
    public void DevelopmentConnectionString_WhenRead_CarriesNoPassword(string keyword)
    {
        ConnectionString.ContainsKey(keyword).ShouldBeFalse(
            $"'{keyword}' appears in a committed file; the password belongs in user-secrets.");
    }

    [Fact]
    public void DevelopmentConnectionString_WhenRead_TargetsTheLocalContainer()
    {
        ConnectionString["Host"].ShouldBe(
            "localhost",
            "the development database is the container published on the host by docker-compose.yml.");
    }

    [Theory]
    [InlineData("Username", "POSTGRES_USER")]
    [InlineData("Database", "POSTGRES_DB")]
    [InlineData("Port", "POSTGRES_PORT")]
    public void DevelopmentConnectionString_WhenRead_MatchesTheEnvironmentTemplate(
        string connectionStringKey,
        string environmentKey)
    {
        ConnectionString[connectionStringKey].ShouldBe(
            EnvironmentTemplate[environmentKey],
            $"'{connectionStringKey}' and '{environmentKey}' describe the same database and must not drift apart.");
    }

    [Fact]
    public void ComposeFile_WhenRead_MountsThePostgresVolumeAboveTheDataDirectory()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");

        compose.ShouldContain(
            "postgres-data:/var/lib/postgresql\n",
            Case.Sensitive,
            "postgres:18 keeps PGDATA in a subdirectory, so mounting /var/lib/postgresql/data " +
            "would leave the real data directory outside the volume.");
    }

    private static string ReadDevelopmentConnectionString()
    {
        var json = ReadRepositoryFile(Path.Combine("src", "Gym.Api", "appsettings.Development.json"));

        // The same options the JSON configuration provider uses, so a file that ASP.NET Core
        // accepts is a file this test accepts.
        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return document.RootElement.GetProperty("ConnectionStrings").GetProperty("Postgres").GetString()
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is null.");
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString) =>
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => pair[0].Trim(),
                pair => pair.Length is 2 ? pair[1].Trim() : string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseEnvironmentFile(string contents) =>
        contents
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#') && line.Contains('=', StringComparison.Ordinal))
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim(), StringComparer.Ordinal);

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

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
