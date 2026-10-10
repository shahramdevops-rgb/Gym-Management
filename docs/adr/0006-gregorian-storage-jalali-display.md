# 0006. Store Gregorian dates and UTC moments; show Jalali only in the browser

- Status: Accepted
- Date: 2026-09-17 (recorded 2026-10-11)

## Context

The gym lives by the Jalali calendar: plans are sold "for a month" in Mehr, rent is due on the
first of the Jalali month, birthdays are celebrated on the Jalali day. The people who use the app
never see a Gregorian date. At the same time, .NET, PostgreSQL, the date functions in SQL and
every library the project uses think in Gregorian dates, and the server's clock is UTC.

Two different things are called "a date" here. A **business date** is a day: the day a plan
starts, a cheque's due date, a birth date. A **moment** is an instant: when a payment was taken,
when a member checked in. Mixing the two is the classic off-by-one-day bug, because 21:00 UTC is
already the next day in Tehran.

## Decision

- **Moments** are stored as UTC `timestamptz`. Nothing reads the system clock directly: every
  handler gets an injected `TimeProvider`, so tests set the time with `FakeTimeProvider`.
- **Business dates** are `DateOnly`, stored as Gregorian `date`, in the gym's time zone
  (`Gym:TimeZone`, `Asia/Tehran`, checked at startup). "Today" comes from `IGymCalendar.Today()`,
  and domain methods take it as a parameter instead of reading a clock.
- **The API is calendar-neutral.** It sends and receives ISO dates (`2026-09-17`) and UTC
  timestamps, and knows nothing about Jalali months.
- **Jalali is a display concern of the frontend.** Dates are shown with `Intl` in the `persian`
  calendar, chosen in Jalali dropdowns (`JalaliDateField`, `BirthDateField`) and converted to ISO
  before they are sent. Ranges such as "this Jalali month" are worked out in the browser and sent
  as Gregorian dates; the dashboard gets figures per day and adds them up into Jalali months.

## Alternatives considered

- **Store Jalali dates** (as text or as year/month/day columns). Every query, index, range and
  date function would need a Jalali implementation, in SQL and in C#, and comparing or sorting
  would be the application's job instead of the database's.
- **Convert to Jalali on the server** and send Persian dates in the API. The server would then own
  a display choice, every test would assert on formatted strings, and a calendar or format change
  would be a backend release.

## Consequences

- Easier: the database, EF Core and the C# date types work as designed; date arithmetic on the
  server is ordinary arithmetic; the API can be tested without any calendar.
- Harder: the frontend carries two calendars (ICU for showing, `date-fns-jalali` for picking), and
  a test checks that they agree. Rules that follow the Jalali calendar (a birthday by Jalali month
  and day, the SMS sent "on the birthday") have to be computed where the Jalali code is, or with
  .NET's `PersianCalendar` when the server must decide.
- Ongoing: every new date input uses the shared Jalali fields, never a raw Gregorian box, and every
  new "today" goes through `IGymCalendar`.
