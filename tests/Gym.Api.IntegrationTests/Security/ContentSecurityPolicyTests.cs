using System.Text.RegularExpressions;

using Gym.Api.IntegrationTests.Infrastructure;

namespace Gym.Api.IntegrationTests.Security;

/// <summary>
/// The panel's Content-Security-Policy (deploy/Caddyfile) refuses every inline script (task 11.2).
/// </summary>
/// <remarks>
/// These checks keep the two halves of that promise together: the policy never grows
/// <c>'unsafe-inline'</c> for scripts, and <c>web/index.html</c> never grows an inline script the
/// policy would then block. Only production sends the header, so nothing in development or the
/// frontend tests would show the break; a white screen on the server would.
/// </remarks>
public sealed partial class ContentSecurityPolicyTests
{
    private const string CaddyfilePath = "deploy/Caddyfile";
    private const string IndexHtmlPath = "web/index.html";

    [Fact]
    public void PanelPolicy_ScriptSource_IsTheFirstPartyOnly() =>
        Directive(PanelPolicy(), "script-src").ShouldBe("script-src 'self'");

    [Fact]
    public void PanelPolicy_FrameAncestors_IsNone() =>
        Directive(PanelPolicy(), "frame-ancestors").ShouldBe("frame-ancestors 'none'");

    [Fact]
    public void IndexHtml_EveryScript_IsAFileNotInline()
    {
        var scripts = ScriptTag().Matches(RepositoryFiles.ReadAllText(IndexHtmlPath));

        scripts.ShouldNotBeEmpty();
        foreach (Match script in scripts)
        {
            script.Groups["attributes"].Value.ShouldContain("src=\"", customMessage: script.Value);
            script.Groups["body"].Value.Trim().ShouldBeEmpty(script.Value);
        }
    }

    [Fact]
    public void IndexHtml_InlineEventHandlers_AreNone() =>
        InlineEventHandler().IsMatch(RepositoryFiles.ReadAllText(IndexHtmlPath)).ShouldBeFalse();

    /// <summary>The first policy in the file is the panel's: its site block comes first.</summary>
    private static string PanelPolicy()
    {
        var match = PolicyHeader().Match(RepositoryFiles.ReadAllText(CaddyfilePath));
        match.Success.ShouldBeTrue($"{CaddyfilePath} has no Content-Security-Policy");

        return match.Groups["policy"].Value;
    }

    private static string? Directive(string policy, string name) =>
        policy.Split(';')
            .Select(part => part.Trim())
            .SingleOrDefault(part => part.StartsWith(name + " ", StringComparison.Ordinal));

    [GeneratedRegex("""Content-Security-Policy "(?<policy>[^"]+)"\s""")]
    private static partial Regex PolicyHeader();

    [GeneratedRegex("""<script\b(?<attributes>[^>]*)>(?<body>[\s\S]*?)</script>""")]
    private static partial Regex ScriptTag();

    [GeneratedRegex("""\son[a-z]+=""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineEventHandler();
}
