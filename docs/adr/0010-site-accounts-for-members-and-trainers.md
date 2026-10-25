# 0010. Let members and trainers sign in on the site, apart from the staff

- Status: Accepted
- Date: 2026-10-25

## Context

The project was founded as a closed system: one Owner, a few Staff, "no public access, no
self-registration, no member logins" (CLAUDE.md, BUSINESS_RULES.md §1, ADR 0003). The panel was
closed on 2026-10-25 with every task of Phases 0 to 13 done, and the work moved to the public site.

On 2026-10-25 the developer, who is also the gym's Owner, decided that the site's main job is the
work between the gym's trainers and their students: daily timetables and attendance, monthly
assessments with the body analysis and progress photos, programs and nutrition. Both sides sign in
on the site with their phone number and a one-time SMS code. The rules are in BUSINESS_RULES.md §15.

That reverses the founding rule, so it is a decision rather than a feature. Four forces shape it:

- **The people are already in the panel.** A student is a gym member: their subscription, their
  sessions and every visit the desk checked in live in the gym's database. The trainer's view of
  attendance is the desk's check-ins, not a second record. A separate product with its own data
  would have to copy members and visits, and keep the copies right.
- **The staff's sign-in must not get weaker.** The panel holds the gym's money. Hundreds of
  members signing in from anywhere must not share a token, a cookie, a rate limit or a policy
  with it, and no mistake in a policy on the members' side may ever reach a staff endpoint.
- **Body photos are the most sensitive data the system will hold**, more than money: who may see
  them is a rule of its own, they are files rather than rows, and they have to be in the backups.
- **The server is small** (2 cores, 2 GB of RAM, 50 GB of disk, ADR 0003) and has no foreign cloud
  service behind it. Whatever is added runs inside the same stack.

## Decision

**One application, one database, two separate sign-ins.**

- **Same monolith and database** (ADR 0001). The site's features are new feature folders in
  Application (for example `Application/Coaching/...`) that use the existing `Member`,
  `Subscription` and `Attendance`; no second service and no copy of the data.
- **A site account is not a staff user.** Site accounts are their own table, keyed by the verified
  phone number, with no password. They are not ASP.NET Identity users (ADR 0002 keeps Identity for
  the staff). A site account links to a `Member`, to a `Trainer`, or to both.
- **A separate authentication scheme.** The site's tokens are issued with their own issuer,
  audience and signing key, and refreshed through their own refresh-token table and cookie on the
  site's host. Every staff policy requires the staff scheme, so a site token cannot satisfy one
  even if a policy is written wrong; every site endpoint has an explicit site policy (`Member`,
  `Trainer`). Rate limits are partitioned per phone and per IP, apart from the staff login's.
- **Its own address and frontend.** The account area lives on its own subdomain,
  `app.pasargadgymplus.ir` (chosen by the developer), as the panel does (6.5.0): its cookie never reaches the panel's host,
  and Caddy can limit it separately. The public page stays static files, so it keeps working while
  the API is down. The frontend is a second entry in `web/` that shares `lib/` (Jalali dates,
  digits, money, error codes, the API client) and none of the panel's pages.
- **A trainer is made by the Owner, in the panel.** Anyone can sign up as a member; nobody can
  sign up as a trainer. The panel gets a small «مربیان» page for it.
- **Files on disk, behind the API.** Photos and analysis PDFs are stored on a Docker volume, with
  their metadata in Postgres. There is no public URL: every download goes through an endpoint that
  checks, at that moment, that the reader may see it. Photos are re-encoded on upload, which also
  drops their EXIF data (a phone photo can carry the place it was taken). `backup.sh` takes the
  files with the database dump, and the restore puts both back.

## Alternatives considered

- **A separate trainer product with a narrow read-only API into the gym** (the advice of
  2026-10-09, when the trainer was thought to be independent of the gym). Right for an outside
  trainer; wrong now that the trainers are the gym's own and the students are its members. It
  would duplicate members, keep a second sign-in, and still need the gym's attendance. Rejected.
- **Members and trainers as Identity users with new roles.** One user table and one sign-in
  pipeline. Rejected because it puts hundreds of outside accounts in the same table, token and
  policy space as the Owner: a single wrong `RequireRole` would open the panel to a member.
  Identity's password, lockout and trusted-device machinery (ADR 0004) also has nothing to do with
  a one-time code.
- **The account area on the public page's own domain, under a path.** One address fewer. Rejected
  for the reasons 6.5.0 gave for the panel's subdomain: cookies and limits stay apart, and the
  static page stays independent of the API.
- **Files in Postgres (`bytea`).** One backup and transactions for free. Rejected: a few hundred
  members with monthly photos make the dump, and every restore, grow by gigabytes on a 2 GB-RAM
  server, for data that is never queried.
- **Reading the analysis numbers out of the PDF.** The Anea BIA sheet is a single image with no
  text (checked on 2026-10-25), so it would need OCR of Persian digits. Rejected for now: the
  trainer types about ten numbers beside the PDF. Anea sells an Excel export (once, with every
  earlier result, matched by phone); not bought for now (2026-10-25). The QR code on the sheet
  cannot be read by a barcode reader.

## Consequences

- CLAUDE.md's "No public access, no self-registration, no member logins" and BUSINESS_RULES.md §1
  "Members never log in" change once this is accepted. The panel stays staff-only.
- Every SMS one-time code is a paid message and the endpoint is public: limits per phone and per
  IP, short-lived codes and few tries are part of the first task, not hardening for later.
- Members appear in the panel who have never been to the desk and hold no plan; the panel's lists
  and the broadcast group «همه اعضای فعال» include them unless a rule says otherwise.
- File storage is new: disk space has to be watched, `backup.sh` and the restore change, and the
  restore drill (docs/DEPLOYMENT.md) has to cover files.
- The panel, closed on 2026-10-25, is opened for the «مربیان» page and the members' sign-up mark,
  each as a numbered task of Phase 15.
- The member's program works offline (decided by the developer): the site's frontend has a
  service worker and a queue of marks made offline, which the panel never had (it decided "no
  offline mode", 2026-10-11). Marks sent late must be accepted with the time they were made.
- An image library will be needed to re-encode photos; it is not in docs/ARCHITECTURE.md yet and
  is asked for in its task.
