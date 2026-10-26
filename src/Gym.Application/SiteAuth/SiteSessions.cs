using Gym.Application.Common;
using Gym.Application.Common.Security;
using Gym.Application.SiteAuth.GetMe;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;

namespace Gym.Application.SiteAuth;

/// <summary>
/// Starts a session for a site account: a new refresh-token family and an access token. Shared by
/// the sign-in and the sign-up, so both hand out exactly the same kind of session.
/// </summary>
public sealed class SiteSessions(IAppDbContext db, ISiteTokens tokens)
{
    /// <summary>
    /// Adds the refresh token to the context; the caller saves it with whatever else the sign-in
    /// changed, in one save.
    /// </summary>
    public SiteSession Start(SiteAccount account, Member member, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);

        var refreshSecret = RefreshTokenSecret.Generate();
        var refreshToken = SiteRefreshToken.Issue(account.Id, RefreshTokenSecret.Hash(refreshSecret), now);
        db.SiteRefreshTokens.Add(refreshToken);

        return For(account, member, refreshSecret, refreshToken.ExpiresAt);
    }

    /// <summary>The session for a refresh secret already made: a sign-in's, or a rotation's child.</summary>
    public SiteSession For(SiteAccount account, Member member, string refreshSecret, DateTimeOffset refreshExpiresAt)
    {
        var access = tokens.IssueAccessToken(account);

        return new SiteSession(
            new SiteAccessResponse(access.Value, access.ExpiresAt, SiteMeResponse.From(member)),
            refreshSecret,
            refreshExpiresAt);
    }
}
