# Learning Journal

Claude appends new concepts here after each task. Add your own notes and questions too.

Format:

## <task id> — <task name>
- **Concept:** one-sentence explanation
- **My notes:**

---

## 0.1 — Repository and solution skeleton

- **The dependency rule:** Clean Architecture is enforced by which project may reference which, so `Gym.Domain` has zero references and every arrow points inward — this is why an entity can never call EF Core.
- **`Directory.Build.props`:** MSBuild walks up from each `.csproj` and imports the nearest one, so `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors` are declared once and cannot drift per project.
- **Layered props files:** MSBuild imports only the *nearest* `Directory.Build.props`, so `tests/Directory.Build.props` must import the root one explicitly with `GetPathOfFileAbove` before adding its test-only settings.
- **Central Package Management:** `Directory.Packages.props` holds every version and `.csproj` files reference packages by name only, so two projects can never disagree on a version.
- **`global.json`:** pins the SDK band (`10.0.401` + `rollForward: latestPatch`) so this machine and CI compile with the same compiler instead of silently diverging.
- **Warnings as errors:** cheap on day one and expensive later, because a codebase with 200 tolerated warnings is one where nobody reads warnings at all.
- **Analyzer rules vs project conventions:** CA1707 forbids underscores in member names, which contradicts the mandated `Method_Scenario_ExpectedResult` test naming; the fix is to scope the rule off for `tests/**` in `.editorconfig`, not to rename the tests or silence the rule globally.
- **Architecture tests:** `Assembly.GetReferencedAssemblies()` turns the dependency rule into an executable assertion, so a forbidden reference fails the test run instead of surviving code review.
- **Trade-off behind those tests:** they read *compiled* references, so they are weak while assemblies are near-empty and get stronger as code lands; the airtight alternative (parsing `.csproj` files) is more brittle, and a dedicated library such as NetArchTest is not on the approved list.
- **Microsoft Testing Platform:** the .NET 10 SDK dropped the VSTest bridge, so xunit.v3 test projects are executables hosting their own runner and `global.json` opts `dotnet test` in with `"test": { "runner": "Microsoft.Testing.Platform" }`.
- **`.gitattributes` and `.editorconfig` must agree:** git on Windows converts to CRLF by default, which would silently contradict `end_of_line = lf`; `* text=auto eol=lf` settles it for both the repo and the working tree.
- **My notes:**
