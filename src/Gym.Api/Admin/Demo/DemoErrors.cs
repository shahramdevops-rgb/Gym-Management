using Gym.Domain.Common;

namespace Gym.Api.Admin.Demo;

/// <summary>
/// Why <c>admin seed-demo</c> refused. Printed on the console only; no endpoint returns these, so
/// they need no Persian message in the frontend.
/// </summary>
internal static class DemoErrors
{
    public static readonly Error NotAllowedHere = Error.BusinessRule(
        "Demo.NotAllowedHere",
        "This is a Production host. The demo runs there only with Demo:Allowed=true (the screenshot stack), never on the gym's server.");

    public static readonly Error NoOwner = Error.BusinessRule(
        "Demo.NoOwner",
        "There is no Owner account. Start the API once with Seed:OwnerUserName and Seed:OwnerPassword set, then run this again.");

    public static readonly Error DatabaseNotEmpty = Error.Conflict(
        "Demo.DatabaseNotEmpty",
        "The database already holds data. The demo only fills an empty one, so it can never mix with real members.");
}
