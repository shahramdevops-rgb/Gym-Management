using System.Net;

using Gym.Api.Authorization;
using Gym.Application.Common;
using Gym.Application.Common.Security;

using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Endpoints;

/// <summary>
/// The site's sign-in code shown on the developer's own computer, so the site app can be tried
/// without a phone (asked by the developer, 1405/08/09): the code screen shows it at the top.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never on the server.</b> <c>Program.cs</c> maps this only in the Development environment,
/// which the production images never run in (<c>ASPNETCORE_ENVIRONMENT: Production</c>), and it
/// answers only a request from this computer itself. In production the path does not exist, so
/// §15's "the code is never shown or stored" holds where it matters.
/// </para>
/// <para>
/// The code is worked out from the stored seed, as the SMS's was (<see cref="ISignInCodes"/>); the
/// API keeps no code anywhere for this.
/// </para>
/// </remarks>
public static class SiteDevelopmentCodeEndpoints
{
    public static IEndpointRouteBuilder MapSiteDevelopmentCodeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{SitePaths.Root}/auth/development-code", async (
                string phoneNumber,
                HttpContext http,
                IPhoneNormalizer phones,
                IAppDbContext db,
                ISignInCodes codes,
                CancellationToken ct) =>
            {
                if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
                {
                    return Results.NotFound();
                }

                var phone = phones.Normalize(phoneNumber);
                if (phone.IsFailure)
                {
                    return Results.NotFound();
                }

                var row = await db.SignInCodes.AsNoTracking().SingleOrDefaultAsync(code => code.PhoneNumber == phone.Value, ct);

                return row is null
                    ? Results.NotFound()
                    : Results.Ok(new DevelopmentCodeResponse(codes.CodeFor(row.PhoneNumber, row.CodeSeed)));
            })
            .AllowAnonymous()
            .WithTags("Site sign-in")
            .WithName("GetSiteDevelopmentCode")
            .Produces<DevelopmentCodeResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record DevelopmentCodeResponse(string Code);
