# 0002. Keep the Identity User in Infrastructure, not Domain

- Status: Accepted
- Date: 2026-09-18

## Context

Staff and the Owner log in with ASP.NET Core Identity, which gives us password hashing,
lockout, security stamps and role storage without writing any of it ourselves. Identity
needs a user class that derives from `IdentityUser<Guid>`.

`IdentityUser<Guid>` is a framework type from `Microsoft.Extensions.Identity.Stores`. ADR 0001
says Domain references no other project and no package, and a test enforces that. So a user
class that derives from it cannot live in Domain.

Its base class also exposes public setters on every property (`UserName`, `PasswordHash`,
`LockoutEnd`, ...), because `UserManager` writes through them. That conflicts with our
private-setter rule for entities no matter where the class lives.

## Decision

`User : IdentityUser<Guid>` lives in `Gym.Infrastructure/Identity/`, next to `Roles` and the
seeder. Our own properties (`FullName`, `IsActive`, `MustChangePassword`) keep private setters
and are set through the constructor, and later through methods on `User`.

`User` is not a `Gym.Domain.Common.Entity`. It has no `CreatedAt`/`UpdatedAt` columns and the
audit interceptor does not stamp it. Changes to users will be recorded by the audit log (task 1.6).

## Alternatives considered

- **User in Domain, deriving from `IdentityUser`.** Breaks the dependency rule and the
  Domain dependency test. Rejected.
- **A pure Domain `User` plus a separate `ApplicationUser` for Identity, mapped to each other.**
  This is the textbook-clean option: Domain would own the user's rules. But it means two
  classes and two tables (or a shared table with fiddly mapping) and a synchronisation step on
  every change. Users have almost no business rules here: active or not, must change
  password or not. Rejected as cost without benefit at this size.
- **Write our own user table and password hashing.** Rejected. Hashing, lockout and
  constant-time comparison are exactly the kind of code that is easy to get subtly wrong.

## Consequences

- Application handlers that manage users (task 1.5) cannot use `User` directly. They go through
  an interface that Application declares and Infrastructure implements, the same as `IAppDbContext`.
- Rules about users (deactivate, force a password change) live as methods on `User` in
  Infrastructure, so they are tested by integration tests, not by `Gym.Domain.Tests`.
- If user rules grow substantially, this is the decision to revisit, with the two-class
  option as the likely replacement.
