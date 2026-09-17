# Gym Management System

Internal gym management system for 1 Owner and few Staff.
No public access, no self-registration, no member logins.

## Where things are
- Roadmap and task status: docs/ROADMAP.md
- Business rules (source of truth): docs/BUSINESS_RULES.md. Read the relevant section before touching Domain or Application code.
- Stack, packages, patterns, gotchas: docs/ARCHITECTURE.md
- Architecture decisions: docs/adr/
- Learning journal: docs/LEARNING.md

## Commands
- Start local infrastructure: `docker compose up -d`
- Build: `dotnet build`
- All tests: `dotnet test`
- Domain tests only: `dotnet test tests/Gym.Domain.Tests`
- Add migration: `dotnet ef migrations add <Name> --project src/Gym.Infrastructure --startup-project src/Gym.Api --output-dir Persistence/Migrations`
- Apply migrations locally: `dotnet ef database update --project src/Gym.Infrastructure --startup-project src/Gym.Api`
- Run API: `dotnet run --project src/Gym.Api`
- Frontend (inside web/): `npm run dev`, `npm run build`, `npm run lint`, `npm test`
- Regenerate API types after any endpoint change (inside web/, API running): `npm run gen:api`
Keep this section up to date when commands change.

## Language and UI
- The UI is Persian (`fa`) and right-to-left. All user-facing text is Persian.
- Code, identifiers, comments, commits, logs, and API error descriptions are English.
- The API returns stable error codes (e.g. `Members.PhoneAlreadyExists`); the frontend maps codes to Persian messages in `web/src/lib/errors.ts`.
- Use logical CSS utilities (`ms-`, `me-`, `ps-`, `pe-`, `start-`, `end-`), never `ml-`, `mr-`, `pl-`, `pr-`, `left-`, `right-`.
- The backend stores Gregorian dates only. The frontend displays Jalali dates and converts Jalali input to ISO dates before sending.
- Show numbers with Persian digits; accept both Persian and English digits in every input.

## Architecture rules (non-negotiable)
- Clean Architecture: Api → Application → Domain. Infrastructure implements Application interfaces. Domain references no other project and no EF Core.
- Organize by feature, not by technical type: `Application/Members/CreateMember/{Command, Validator, Handler, Response}.cs`.
- Do NOT use MediatR, AutoMapper, FluentAssertions, or generic repositories. Handlers are plain injected classes, mapping is manual, Application uses `IAppDbContext`.
- Minimal APIs: one endpoints file per feature in `Gym.Api/Endpoints/`, using `MapGroup`. Every endpoint has an explicit authorization policy.
- Business failures return `Result`/`Error`. Exceptions are only for unexpected failures. The API maps errors to ProblemDetails.
- Entities have private setters. Rules live in entity methods (e.g. `subscription.ConsumeSession(today)`).
- Never call `DateTime.Now` or `DateTime.UtcNow`. Inject `TimeProvider`.
- Moments are UTC `timestamptz`. Business dates are `DateOnly` in the gym's configured time zone.
- Money is `decimal` stored as `numeric(18,2)`. Never float or double.
- Financial records are never hard-deleted. Use refund or void entries with a reason.
- IMPORTANT: invariants are enforced by the database too (unique and partial unique indexes, check constraints, `xmin` concurrency), not only by code.

## Testing
- IMPORTANT: every task ends with passing tests. Show the `dotnet test` output as evidence.
- Domain rules → unit tests in `Gym.Domain.Tests` (no database).
- Endpoints, transactions, constraints, concurrency → integration tests using Testcontainers Postgres. Never use the EF Core InMemory provider.
- Use `FakeTimeProvider` to control dates. Assertions: Shouldly. Mocks: NSubstitute, only at infrastructure boundaries.
- Test names: `Method_Scenario_ExpectedResult`.

## Workflow
- One roadmap task per session. Do not start the next task unless asked.
- For any non-trivial task, present a plan first and wait for approval.
- If a business rule is unclear or missing from docs/BUSINESS_RULES.md, stop and ask. Never invent business rules.
- Do not add packages that are not listed in docs/ARCHITECTURE.md without asking.
- Warnings are errors. Fix them; do not suppress them.
- Never commit secrets. Local secrets use `dotnet user-secrets`; production uses environment variables.
- Commit messages follow Conventional Commits, e.g. `feat(members): add phone normalization`. Ask before committing.

## Teaching mode
The developer is learning .NET architecture through this project.
- Prefer clear, conventional code over clever code.
- After finishing a task: explain briefly what was built and why, list the new concepts with one sentence each, and append them to docs/LEARNING.md under the task ID.
- When the developer asks "why", answer with the trade-off, not just the rule.
