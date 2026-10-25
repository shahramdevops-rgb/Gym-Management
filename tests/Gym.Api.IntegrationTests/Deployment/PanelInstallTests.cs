using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Deployment;

/// <summary>
/// The panel installs on a phone or a computer as an app of its own (roadmap 13.4).
/// </summary>
/// <remarks>
/// Nothing in development shows a broken manifest: the panel works the same in a browser tab, and
/// Chrome quietly falls back to a plain shortcut with its own badge on the icon, which is what the
/// developer's phone showed before this task. So the files are checked here, as SearchIndexingTests
/// does for robots.txt.
/// </remarks>
public sealed partial class PanelInstallTests
{
    private const string ManifestPath = "web/public/manifest.webmanifest";
    private const string PanelIndexPath = "web/index.html";
    private const string CaddyfilePath = "deploy/Caddyfile";

    /// <summary>About what a phone's home screen writes under an icon before cutting it short.</summary>
    private const int HomeScreenLabelLength = 12;

    [Fact]
    public void Manifest_Display_IsStandaloneFromTheRoot()
    {
        var manifest = Manifest();

        manifest.GetProperty("display").GetString().ShouldBe("standalone");
        manifest.GetProperty("start_url").GetString().ShouldBe("/");
        manifest.GetProperty("scope").GetString().ShouldBe("/");
        manifest.GetProperty("id").GetString().ShouldBe("/");
    }

    [Fact]
    public void Manifest_Names_AreTheDevelopersAndFitUnderTheIcon()
    {
        var manifest = Manifest();

        manifest.GetProperty("name").GetString().ShouldBe("Pasargad Gym Management");
        var shortName = manifest.GetProperty("short_name").GetString();
        shortName.ShouldBe("Management");
        shortName!.Length.ShouldBeLessThanOrEqualTo(HomeScreenLabelLength);
    }

    [Fact]
    public void Manifest_Icons_ExistAtTheSizeTheyClaim()
    {
        var icons = Manifest().GetProperty("icons").EnumerateArray().ToList();

        icons.ShouldNotBeEmpty();
        foreach (var icon in icons)
        {
            var source = icon.GetProperty("src").GetString()!;
            var size = PngSize("web/public" + source);
            icon.GetProperty("sizes").GetString().ShouldBe($"{size.Width}x{size.Height}", source);
            icon.GetProperty("type").GetString().ShouldBe("image/png", source);
        }
    }

    [Fact]
    public void Manifest_Icons_CoverWhatAndroidAsksFor()
    {
        var icons = Manifest().GetProperty("icons").EnumerateArray()
            .Select(icon => (Sizes: icon.GetProperty("sizes").GetString(), Purpose: icon.GetProperty("purpose").GetString()))
            .ToList();

        icons.ShouldContain(("192x192", "any"));
        icons.ShouldContain(("512x512", "any"));
        icons.ShouldContain(("512x512", "maskable"));
    }

    [Fact]
    public void Manifest_Shortcuts_AreTheDesksPagesAndTheOwnersSummary()
    {
        // The manifest is the same for everyone, so a shortcut to an Owner-only page would open a
        // refusal for Staff. «خلاصهٔ امروز» is the one exception (13.5): its route sends Staff to
        // the locker map instead (RequireRole's `otherwise`). It comes first, ahead of the desk's three.
        var shortcuts = Manifest().GetProperty("shortcuts").EnumerateArray()
            .Select(shortcut => (Name: shortcut.GetProperty("name").GetString(), Url: shortcut.GetProperty("url").GetString()))
            .ToList();

        shortcuts.ShouldBe([("خلاصهٔ امروز", "/today"), ("ورود با کمد", "/"), ("داخل باشگاه", "/attendance"), ("اعضا", "/members")]);
    }

    [Fact]
    public void Manifest_Shortcuts_EachHasAnIconThatExists()
    {
        // Android may leave out a shortcut that has no icon of its own.
        foreach (var shortcut in Manifest().GetProperty("shortcuts").EnumerateArray())
        {
            var name = shortcut.GetProperty("name").GetString();
            var icon = shortcut.GetProperty("icons").EnumerateArray().ShouldHaveSingleItem(name);
            var size = PngSize("web/public" + icon.GetProperty("src").GetString());
            icon.GetProperty("sizes").GetString().ShouldBe($"{size.Width}x{size.Height}", name);
        }
    }

    [Fact]
    public void PanelIndexHtml_InstallTags_ArePresent()
    {
        var html = RepositoryFiles.ReadAllText(PanelIndexPath);

        html.ShouldContain("<link rel=\"manifest\" href=\"/manifest.webmanifest\" />");
        html.ShouldContain("<link rel=\"apple-touch-icon\" href=\"/apple-touch-icon.png\" />");
        html.ShouldContain("<meta name=\"apple-mobile-web-app-title\" content=\"Management\" />");
        html.ShouldContain("<meta name=\"theme-color\" content=\"#f7f9fc\" />");
    }

    [Fact]
    public void PanelIndexHtml_ThemeColour_ComesBeforeTheThemeScript()
    {
        // theme-init.js darkens the status bar before the first paint; the tag must exist by then.
        var html = RepositoryFiles.ReadAllText(PanelIndexPath);

        html.IndexOf("name=\"theme-color\"", StringComparison.Ordinal)
            .ShouldBeLessThan(html.IndexOf("/theme-init.js", StringComparison.Ordinal));
    }

    [Fact]
    public void ThemeColours_EveryCopy_IsTheSame()
    {
        // The colours live in four files: theme.ts (every switch), theme-init.js (before the first
        // paint), index.html (light, before any script) and the manifest (the dark icon's opening
        // screen). theme.ts is the one read here; the others must hold its values.
        var match = ThemeBarColours().Match(RepositoryFiles.ReadAllText("web/src/lib/theme.ts"));
        match.Success.ShouldBeTrue("web/src/lib/theme.ts has no themeBarColour");
        var light = match.Groups["light"].Value;
        var dark = match.Groups["dark"].Value;

        RepositoryFiles.ReadAllText(PanelIndexPath)
            .ShouldContain($"<meta name=\"theme-color\" content=\"{light}\" />");
        RepositoryFiles.ReadAllText("web/public/theme-init.js").ShouldContain($"\"{dark}\"");
        var manifest = Manifest();
        manifest.GetProperty("background_color").GetString().ShouldBe(dark);
        manifest.GetProperty("theme_color").GetString().ShouldBe(dark);
    }

    [Fact]
    public void AppleTouchIcon_Size_IsWhatAnIphoneUses() =>
        PngSize("web/public/apple-touch-icon.png").ShouldBe((180, 180));

    [Fact]
    public void PanelSite_Manifest_IsServedAsManifestJson()
    {
        var caddyfile = RepositoryFiles.ReadAllText(CaddyfilePath);

        caddyfile.ShouldContain("@manifest path /manifest.webmanifest");
        caddyfile.ShouldContain("header @manifest Content-Type \"application/manifest+json\"");
    }

    /// <summary>theme.ts's two status-bar colours, light then dark.</summary>
    [GeneratedRegex(@"themeBarColour[^=]*= \{ light: ""(?<light>#[0-9a-f]{6})"", dark: ""(?<dark>#[0-9a-f]{6})"" \}")]
    private static partial Regex ThemeBarColours();

    private static JsonElement Manifest() =>
        JsonDocument.Parse(RepositoryFiles.ReadAllText(ManifestPath)).RootElement;

    /// <summary>A PNG's width and height, from its IHDR chunk (bytes 16 to 23, big-endian).</summary>
    private static (int Width, int Height) PngSize(string relativePath)
    {
        var bytes = RepositoryFiles.ReadAllBytes(relativePath);
        bytes.Length.ShouldBeGreaterThan(24, relativePath);
        bytes[..8].ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, relativePath);

        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }
}
