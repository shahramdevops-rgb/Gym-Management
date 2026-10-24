using System.Text.RegularExpressions;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Security;

/// <summary>
/// The panel stays out of search results while the public page is listed (roadmap 14.5).
/// </summary>
/// <remarks>
/// Only production serves these files and headers, so a slip shows nowhere until a search engine
/// lists the staff login or drops the gym's page. The trap these checks guard against: a
/// <c>Disallow: /</c> on the panel looks stricter, but a crawler that may not fetch a page never
/// reads its <c>noindex</c>.
/// </remarks>
public sealed partial class SearchIndexingTests
{
    private const string CaddyfilePath = "deploy/Caddyfile";
    private const string PanelIndexPath = "web/index.html";
    private const string PanelRobotsPath = "web/public/robots.txt";
    private const string SiteIndexPath = "web/public-site/index.html";
    private const string SiteRobotsPath = "web/public-site/robots.txt";
    private const string SiteMapPath = "web/public-site/sitemap.xml";
    private const string SiteAddress = "https://pasargadgymplus.ir/";

    [Fact]
    public void PanelSite_RobotsHeader_IsNoIndex() =>
        SiteBlock("{$DOMAIN} {").ShouldContain("X-Robots-Tag \"noindex, nofollow\"");

    [Fact]
    public void PanelIndexHtml_RobotsMeta_IsNoIndex() =>
        RepositoryFiles.ReadAllText(PanelIndexPath)
            .ShouldContain("<meta name=\"robots\" content=\"noindex, nofollow\" />");

    [Fact]
    public void PanelRobotsTxt_Crawling_IsNotBlocked() =>
        DisallowRule().IsMatch(RepositoryFiles.ReadAllText(PanelRobotsPath)).ShouldBeFalse();

    [Fact]
    public void PublicSite_RobotsHeader_IsAbsent() =>
        SiteBlock("{$PUBLIC_DOMAIN:no-public-site} {").ShouldNotContain("X-Robots-Tag");

    [Fact]
    public void PublicIndexHtml_NoIndex_IsAbsentAndTheCanonicalIsTheDomain()
    {
        var html = RepositoryFiles.ReadAllText(SiteIndexPath);

        html.ShouldNotContain("noindex");
        html.ShouldContain($"<link rel=\"canonical\" href=\"{SiteAddress}\" />");
    }

    [Fact]
    public void PublicRobotsTxt_CrawlingAllowed_AndPointsToTheSitemap()
    {
        var robots = RepositoryFiles.ReadAllText(SiteRobotsPath);

        DisallowRule().IsMatch(robots).ShouldBeFalse();
        robots.ShouldContain($"Sitemap: {SiteAddress}sitemap.xml");
    }

    [Fact]
    public void PublicSitemap_Page_IsTheDomain() =>
        RepositoryFiles.ReadAllText(SiteMapPath).ShouldContain($"<loc>{SiteAddress}</loc>");

    /// <summary>One site block of the Caddyfile: from its opening line to the next site's.</summary>
    private static string SiteBlock(string openingLine)
    {
        var caddyfile = RepositoryFiles.ReadAllText(CaddyfilePath);
        var start = caddyfile.IndexOf("\n" + openingLine, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"{CaddyfilePath} has no site block '{openingLine}'");

        var end = NextSiteBlock().Match(caddyfile, start + 1);
        return end.Success ? caddyfile[start..end.Index] : caddyfile[start..];
    }

    /// <summary>A rule that blocks anything at all: <c>Disallow:</c> followed by a path.</summary>
    [GeneratedRegex(@"^\s*Disallow:\s*\S", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex DisallowRule();

    /// <summary>A site block starts at the beginning of a line, outside any other block.</summary>
    [GeneratedRegex(@"\n(?=[^\s#}][^\n]*\{\s*\n)")]
    private static partial Regex NextSiteBlock();
}
