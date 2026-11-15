using System.Text.RegularExpressions;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Deployment;

/// <summary>
/// The shop's showcase on the gym's public address (roadmap 16.4): the public host hands the API
/// exactly one path, the shop's own, and serves the shop's page under <c>/shop/</c>.
/// </summary>
/// <remarks>
/// Only production reads deploy/Caddyfile, so a slip shows nowhere before the server: a wider
/// <c>/api/*</c> here would put the staff's login and the app's sign-in on the gym's public
/// address, and a missing page fallback would make every product's address a 404. The files are
/// checked here, as <see cref="PanelInstallTests"/> and SearchIndexingTests do for theirs.
/// </remarks>
public sealed partial class PublicHostShopTests
{
    private const string CaddyfilePath = "deploy/Caddyfile";
    private const string PublicBlockOpening = "{$PUBLIC_DOMAIN:no-public-site} {";
    private const string AppBlockOpening = "{$SITE_DOMAIN:no-site-app} {";
    private const string ShopIndexPath = "web/site/shop/index.html";
    private const string SiteMapPath = "web/public-site/sitemap.xml";

    [Fact]
    public void PublicHost_OfTheApi_ProxiesTheShopsShowcaseOnly()
    {
        var block = SiteBlock(PublicBlockOpening);

        var proxied = ProxiedPath().Matches(block).Select(match => match.Groups["path"].Value).ToList();

        proxied.ShouldBe(["/api/site/shop/*"]);
        ReverseProxy().Count(block).ShouldBe(1, "one proxy rule on the public host: the shop's showcase.");
    }

    [Fact]
    public void PublicHost_TheRestOfTheApi_IsNotFound()
    {
        var block = SiteBlock(PublicBlockOpening);

        block.ShouldContain("handle /api/* {\n\t\trespond 404\n\t}");
        // The more specific path is written first only for the reader: Caddy sorts handle blocks
        // by how specific their path is, so the shop's wins either way.
        block.IndexOf("handle /api/site/shop/*", StringComparison.Ordinal)
            .ShouldBeLessThan(block.IndexOf("handle /api/* {", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicHost_TheShopsRequests_GoOnUnderTheAppsHostName()
    {
        // The API answers to the panel's and the app's hosts only (AllowedHosts): under the public
        // host's own name every request of the showcase would be refused with 400.
        SiteBlock(PublicBlockOpening).ShouldContain("header_up Host {$SITE_DOMAIN:no-site-app}");
        RepositoryFiles.ReadAllText("docker-compose.prod.yml")
            .ShouldContain("AllowedHosts: ${DOMAIN:?set DOMAIN in .env};${SITE_DOMAIN:?set SITE_DOMAIN in .env}\n");
    }

    [Fact]
    public void PublicHost_EveryAddressUnderShop_IsTheShopsPage()
    {
        var block = SiteBlock(PublicBlockOpening);

        block.ShouldContain("redir /shop /shop/ 302");
        block.ShouldContain("handle /shop/* {\n\t\troot * /srv-site\n\t\theader Cache-Control \"no-cache\"\n\t\ttry_files {path} /shop/index.html");
        block.ShouldContain("handle /assets/* {\n\t\troot * /srv-site");
    }

    [Fact]
    public void AppHost_TheShop_IsSentToThePublicHost() =>
        SiteBlock(AppBlockOpening)
            .ShouldContain("@shop path /shop /shop/*\n\tredir @shop https://{$PUBLIC_DOMAIN:no-public-site}{uri} 302");

    [Fact]
    public void ShopIndexHtml_IsMeantToBeFound()
    {
        var html = RepositoryFiles.ReadAllText(ShopIndexPath);

        html.ShouldNotContain("noindex");
        SiteBlock(PublicBlockOpening).ShouldNotContain("X-Robots-Tag");
        RepositoryFiles.ReadAllText(SiteMapPath).ShouldContain("<loc>https://pasargadgymplus.ir/shop/</loc>");
    }

    /// <summary>One site block of the Caddyfile: from its opening line to the next site's.</summary>
    private static string SiteBlock(string openingLine)
    {
        var caddyfile = RepositoryFiles.ReadAllText(CaddyfilePath).ReplaceLineEndings("\n");
        var start = caddyfile.IndexOf("\n" + openingLine, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"{CaddyfilePath} has no site block '{openingLine}'");

        var end = NextSiteBlock().Match(caddyfile, start + 1);
        return end.Success ? caddyfile[start..end.Index] : caddyfile[start..];
    }

    /// <summary>A <c>handle</c> block whose first directive is a reverse proxy, with its path.</summary>
    [GeneratedRegex(@"handle (?<path>\S+) \{\s*reverse_proxy ")]
    private static partial Regex ProxiedPath();

    [GeneratedRegex(@"^\s*reverse_proxy ", RegexOptions.Multiline)]
    private static partial Regex ReverseProxy();

    /// <summary>A site block starts at the beginning of a line, outside any other block.</summary>
    [GeneratedRegex(@"\n(?=[^\s#}][^\n]*\{\s*\n)")]
    private static partial Regex NextSiteBlock();
}
