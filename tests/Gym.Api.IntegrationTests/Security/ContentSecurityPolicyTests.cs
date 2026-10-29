using System.Text.RegularExpressions;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Security;

/// <summary>
/// The panel's and the site app's Content-Security-Policy (deploy/Caddyfile) refuse every inline
/// script (task 11.2, roadmap 15.3b).
/// </summary>
/// <remarks>
/// These checks keep the two halves of that promise together: neither policy ever grows
/// <c>'unsafe-inline'</c> for scripts, and neither page (<c>web/index.html</c>,
/// <c>web/site/index.html</c>) ever grows an inline script the policy would then block. Only
/// production sends the header, so nothing in development or the frontend tests would show the
/// break; a white screen on the server would.
/// </remarks>
public sealed partial class ContentSecurityPolicyTests
{
    private const string CaddyfilePath = "deploy/Caddyfile";

    public static TheoryData<string> Pages => ["web/index.html", "web/site/index.html"];

    [Fact]
    public void PanelPolicy_ScriptSource_IsTheFirstPartyOnly() =>
        Directive(PanelPolicy(), "script-src").ShouldBe("script-src 'self'");

    [Fact]
    public void PanelPolicy_FrameAncestors_IsNone() =>
        Directive(PanelPolicy(), "frame-ancestors").ShouldBe("frame-ancestors 'none'");

    [Fact]
    public void SitePolicy_ScriptSource_IsTheFirstPartyOnly() =>
        Directive(SitePolicy(), "script-src").ShouldBe("script-src 'self'");

    [Fact]
    public void SitePolicy_FrameAncestors_IsNone() =>
        Directive(SitePolicy(), "frame-ancestors").ShouldBe("frame-ancestors 'none'");

    [Theory]
    [MemberData(nameof(Pages))]
    public void IndexHtml_EveryScript_IsAFileNotInline(string page)
    {
        var scripts = ScriptTag().Matches(RepositoryFiles.ReadAllText(page));

        scripts.ShouldNotBeEmpty();
        foreach (Match script in scripts)
        {
            script.Groups["attributes"].Value.ShouldContain("src=\"", customMessage: script.Value);
            script.Groups["body"].Value.Trim().ShouldBeEmpty(script.Value);
        }
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public void IndexHtml_InlineEventHandlers_AreNone(string page) =>
        InlineEventHandler().IsMatch(RepositoryFiles.ReadAllText(page)).ShouldBeFalse();

    /// <summary>The first policy in the file is the panel's: its site block comes first.</summary>
    private static string PanelPolicy() => PolicyAfter(0);

    /// <summary>The first policy after the site app's block opens (<c>{$SITE_DOMAIN...} {</c>).</summary>
    private static string SitePolicy()
    {
        var caddyfile = RepositoryFiles.ReadAllText(CaddyfilePath);
        var block = SiteBlock().Match(caddyfile);
        block.Success.ShouldBeTrue($"{CaddyfilePath} has no site block for SITE_DOMAIN");

        return PolicyAfter(block.Index);
    }

    private static string PolicyAfter(int start)
    {
        var match = PolicyHeader().Match(RepositoryFiles.ReadAllText(CaddyfilePath), start);
        match.Success.ShouldBeTrue($"{CaddyfilePath} has no Content-Security-Policy");

        return match.Groups["policy"].Value;
    }

    private static string? Directive(string policy, string name) =>
        policy.Split(';')
            .Select(part => part.Trim())
            .SingleOrDefault(part => part.StartsWith(name + " ", StringComparison.Ordinal));

    [GeneratedRegex("""Content-Security-Policy "(?<policy>[^"]+)"\s""")]
    private static partial Regex PolicyHeader();

    [GeneratedRegex("""^\{\$SITE_DOMAIN[^}]*\} \{""", RegexOptions.Multiline)]
    private static partial Regex SiteBlock();

    [GeneratedRegex("""<script\b(?<attributes>[^>]*)>(?<body>[\s\S]*?)</script>""")]
    private static partial Regex ScriptTag();

    [GeneratedRegex("""\son[a-z]+=""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineEventHandler();
}
