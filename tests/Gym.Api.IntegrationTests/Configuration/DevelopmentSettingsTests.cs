using System.Globalization;
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
    public void SeqSink_WhenRead_TargetsThePortTheEnvironmentTemplatePublishes()
    {
        var serverUrl = ReadDevelopmentSeqServerUrl();

        new Uri(serverUrl).Port.ToString(CultureInfo.InvariantCulture).ShouldBe(
            EnvironmentTemplate["SEQ_INGESTION_PORT"],
            "Serilog would otherwise post to a port nothing is listening on, and the only "
            + "symptom would be an empty Seq with no error anywhere.");
    }

    [Fact]
    public void SeqSink_WhenRead_TargetsTheLocalContainer()
    {
        new Uri(ReadDevelopmentSeqServerUrl()).Host.ShouldBe("localhost");
    }

    [Fact]
    public void CorsOrigins_WhenRead_AreConfiguredForTheViteDevServer()
    {
        var json = ReadRepositoryFile(Path.Combine("src", "Gym.Api", "appsettings.Development.json"));

        using var document = ParseJson(json);

        var origins = document.RootElement
            .GetProperty("Cors")
            .GetProperty("AllowedOrigins")
            .EnumerateArray()
            .Select(origin => origin.GetString())
            .ToArray();

        // AddVitePolicy throws on an empty list, so this asserts the committed file actually
        // carries a value rather than relying on that exception being noticed at runtime.
        origins.ShouldNotBeEmpty();
        origins.ShouldAllBe(origin => origin!.StartsWith("http://localhost", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionSettings_WhenRead_CarryNoSeqSinkAndNoCorsOrigins()
    {
        var json = ReadRepositoryFile(Path.Combine("src", "Gym.Api", "appsettings.json"));

        using var document = ParseJson(json);

        // appsettings.json is the production baseline. A Seq URL or a dev-server origin
        // leaking into it would ship a developer's machine as configuration.
        json.ShouldNotContain("5341", Case.Sensitive);
        document.RootElement.TryGetProperty("Cors", out _).ShouldBeFalse();
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

        using var document = ParseJson(json);

        return document.RootElement.GetProperty("ConnectionStrings").GetProperty("Postgres").GetString()
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is null.");
    }

    private static string ReadDevelopmentSeqServerUrl()
    {
        var json = ReadRepositoryFile(Path.Combine("src", "Gym.Api", "appsettings.Development.json"));

        using var document = ParseJson(json);

        return document.RootElement
            .GetProperty("Serilog")
            .GetProperty("WriteTo")
            .GetProperty("Seq")
            .GetProperty("Args")
            .GetProperty("serverUrl")
            .GetString()
            ?? throw new InvalidOperationException("The Seq sink has no serverUrl.");
    }

    /// <summary>
    /// The same options the JSON configuration provider uses, so a file that ASP.NET Core
    /// accepts is a file these tests accept.
    /// </summary>
    private static JsonDocument ParseJson(string json) =>
        JsonDocument.Parse(
            json,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

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
