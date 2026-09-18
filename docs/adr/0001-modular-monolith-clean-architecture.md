# 0001. Build a modular monolith with Clean Architecture

- Status: Accepted
- Date: 2026-09-18

## Context

The system runs one gym. It is used by one Owner and a few Staff members, on one server,
with no public access. It is maintained by one developer who is also learning .NET
architecture through it.

The domain is small in users but not in rules: subscriptions consume sessions and can be
frozen, payments can be partial and are never deleted, lockers and attendance have
time-of-day rules, and every business date depends on the gym's time zone
(docs/BUSINESS_RULES.md). Those rules must be testable without a database, and they must
not be scattered across endpoints and UI code.

Two forces pull in opposite directions. The scale argues for the simplest possible
deployment. The rule density argues for firm boundaries inside the code.

## Decision

One deployable ASP.NET Core application and one PostgreSQL database, structured as
Clean Architecture and organized by feature inside each layer:

```
Api  →  Application  →  Domain
          ↑
    Infrastructure (implements Application interfaces)
```

- **Domain** holds entities and their rules (`subscription.ConsumeSession(today)`). It
  references no other project and no EF Core, so its tests need nothing but the code.
- **Application** holds one folder per use case (`Members/CreateMember/{Command, Validator,
  Handler, Response}`) and talks to the database through `IAppDbContext`.
- **Infrastructure** implements those interfaces: EF Core, Identity, SMS, background jobs.
- **Api** is Minimal APIs, one endpoints file per feature, mapping `Result` errors to
  ProblemDetails.

Project references enforce the direction: Domain cannot reach Infrastructure because the
compiler does not let it.

Some common companions of this style are deliberately left out:

- **No MediatR.** Handlers are plain classes injected into endpoints. "Go to definition"
  leads straight to the code that runs.
- **No AutoMapper.** Mapping is written by hand, so a renamed property is a compile error
  rather than a silently empty field.
- **No generic repository.** EF Core's `DbContext` already is a unit of work with
  repositories. Wrapping it hides the query features (projections, `AsNoTracking`,
  `ExecuteUpdate`) behind a lowest-common-denominator interface.

## Alternatives considered

- **Microservices.** Independent deployment and scaling solve problems this system does not
  have. The price is real: network failures between services, distributed transactions
  where a payment and a subscription must change together, and several things to deploy
  and monitor on one small server.
- **A single project with folders.** Fastest to start. But nothing stops an endpoint from
  putting a business rule next to its SQL, and the layering exists only as long as everyone
  remembers it. The first rule tested through HTTP because it lives in an endpoint makes the
  whole test suite slower.
- **Vertical slices without layers.** Each feature owns its request, handler and data
  access, which keeps related code together. That idea is kept (organization by feature),
  but without a separate Domain project the rules for subscriptions and payments would
  drift into handlers and be duplicated between them.

## Consequences

- Easier: domain rules are unit-tested in milliseconds; each use case is found in one
  folder; one deployment, one database, one transaction for work that must be atomic.
- Easier: modules can be extracted later if a real reason appears, because features
  already sit behind their own folders and interfaces.
- Harder: more projects and more files per use case than a small app strictly needs, and
  a new contributor has to learn where each kind of code goes.
- Harder: the boundary between modules inside one database is a convention. A handler in
  one feature can still query another feature's tables. Code review has to watch for it.
- Ongoing: database-enforced invariants (unique and partial indexes, check constraints,
  `xmin` concurrency) back up the domain rules, because one database shared by every
  feature is also the last line of defence.
