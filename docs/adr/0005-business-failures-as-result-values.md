# 0005. Return business failures as Result values; throw only for the unexpected

- Status: Accepted
- Date: 2026-09-18 (recorded 2026-10-11)

## Context

Most of what the front desk does can be refused for an ordinary reason: the phone number belongs
to another member, the plan has no sessions left, the locker was taken a moment ago, the payment
is more than what is owed. These are not faults. They are answers the desk has to show, in
Persian, next to the field or the button that caused them.

C# offers two ways to say "this did not happen": throw an exception, or return a value that says
so. The choice decides what a handler's signature promises, how an endpoint turns a refusal into
an HTTP response, and whether "this phone already exists" can be told apart from "the database is
down" in the logs.

## Decision

Every expected failure is a value. Domain methods and handlers return `Result` or `Result<T>`
(`Gym.Domain/Common/Result.cs`), and a failure carries an `Error`: a stable code such as
`Members.PhoneAlreadyExists`, an English description for developers, and an `ErrorType`.

- `ResultExtensions.ToHttpResult()` in Gym.Api is the only place that chooses a status code
  (Validation 400, NotFound 404, Conflict 409, BusinessRule 422, ...) and writes RFC 9457
  ProblemDetails with the code in it.
- The frontend maps each code to a Persian message in `web/src/lib/errors.ts`, and
  `ErrorCatalogTests` fails when a code has no message.
- Exceptions are for what nobody planned for: a lost connection, a bug. `GlobalExceptionHandler`
  turns them into a 500 with only a correlation id, and they are logged at Error.
- A database constraint that is expected to fire in a race (a unique index, the subscription
  exclusion constraint, `xmin`) is caught where it can happen and translated into the same `Error`
  the code check returns, so the desk hears one message whichever check caught it.

## Alternatives considered

- **Exceptions for business failures**, with a middleware mapping exception types to status codes.
  Less code per handler. But the signature `Task<MemberResponse>` hides that the call can be
  refused, the compiler does not make anyone handle it, every refusal unwinds a stack, and the logs
  mix "phone already exists" with real faults, so an alert on exceptions would fire all day.

## Consequences

- Easier: a handler's signature says it can fail; tests assert on `result.Error.Code` instead of
  catching; one mapping decides every status code; the error codes are a contract the frontend and
  a test both check.
- Harder: every caller unwraps a `Result`, and code that chains several steps has to pass each
  failure along by hand. That is the point, but it is more lines than `throw`.
- Ongoing: a new error needs a code, an `ErrorType` and a Persian message, or `ErrorCatalogTests`
  fails. Console-only errors (the server commands) stay in Gym.Api and are printed, not mapped.
