# 0011. Run the online shop inside the system, with a shop admin role in the panel

- Status: Accepted
- Date: 2026-11-10

## Context

On 2026-11-10 (1405/08/19) the developer, who is also the gym's Owner, decided to sell online on
the gym's main site (roadmap Phase 16, BUSINESS_RULES.md §16):

- Buyers from anywhere in Iran pay online on the site, and the goods reach them by courier (پیک) or
  Tipax.
- The shop is **an extra online sale, nothing more**: its money, stock and refunds never meet the
  panel's till, dashboard, reports or the counter's shop items (6.5.29). Its stock is typed by hand.
- A **shop admin** runs it alone: categories, products and their photos, stock, orders, shipping,
  and messages with customers. The Owner adds this person like a staff account, with a user name and
  a password, and they get a panel of their own for the shop.
- It lives on the gym's site, not on a separate shop site.

Three things make this a decision rather than a feature:

- **The panel was founded for one Owner and a few Staff** (CLAUDE.md, BUSINESS_RULES.md §1). A third
  kind of staff user, who must see none of the gym's members or money, changes that.
- **Customers are outside people**, most of them never at the gym. ADR 0010 already gives outside
  people a sign-in (the site's phone and one-time code), but a site sign-up today creates a gym
  member in the panel (§15).
- **Online payment is money moving through an outside party.** A payment the gateway confirmed and
  the system did not record, or one recorded twice, is a real loss or a real dispute.

## Decision

**One application and one database, as ADR 0001 and 0010; a new `Shop` role on the staff side,
and customers on the site side.**

- **Same monolith and database.** The shop is new feature folders (`Application/Shop/...`) with its
  own tables. No shop table references `ServiceCharge`, `Payment` or any of the panel's money, and no
  sum in the dashboard or the reports reads the shop's tables; the panel shows the online sales
  on a read-only page of their own.
- **The shop admin is an Identity user with a new role, `Shop`**, made by the Owner on the staff
  page with the same password, lockout and trusted-device rules (ADR 0004). They sign in on the
  panel's address with the staff scheme. Every existing staff policy names its roles
  (`OwnerOnly`, `StaffOrOwner`), so the new role passes none of them; the shop's endpoints get their
  own policy, `ShopAdmin`. The panel's frontend shows a user with this role a layout of its own,
  with the shop's pages only.
- **Customers are site accounts** (ADR 0010): they sign in with their phone and a one-time code, and
  one account serves the shop and the gym. A person who signed up on the site and has never had a
  plan or a visit is a **site user** (§16 *Site users*): left out of every list, search and figure
  of the gym's, and shown only on a page of their own, «کاربران سایت». The shop admin sees of a
  customer only what an order needs.
- **Where the customer's shop lives: on the gym's main domain** (decided by the developer at the
  start of 16.4, 2026-11-15, replacing this ADR's first proposal, the site app). The public
  page's «فروشگاه» section shows the products, and the shop's own pages are under
  `pasargadgymplus.ir/shop/`. They are a second page of the site app's build (`web/site/shop`,
  code in `web/src/site/shop`), served by Caddy on the public host, which hands the API one path
  only, `/api/site/shop` (anonymous, read-only, rate limited). The public page keeps working
  while the API is down: if the products cannot be fetched its section keeps its placeholder
  shelves.
  - Why not the app's host: the eNamad is given to a domain and the payment gateway must start and
    end a payment on the domain it is registered for; the main domain is safe whether or not a
    sub-domain would have counted. The app's host is also closed to search engines.
  - What it costs: the gym's public address is no longer "static files, no API": it reaches one
    read-only path. And the customer's sign-in for buying (16.5) has to work on the main domain,
    where no site session exists today (the app's refresh cookie belongs to its own host); how is
    16.5's first question.
- **Payment behind an Application interface** (`IPaymentGateway`), implemented in Infrastructure
  for Zarinpal (chosen by the developer), against its sandbox until the eNamad and the real merchant account exist. A payment is
  verified by the server with the gateway, never trusted from the browser's return; each payment
  attempt is its own row, the gateway's reference is unique in the database, and verifying twice
  records once.
- **Product photos use ADR 0010's file storage** (a Docker volume, metadata in Postgres, re-encoded
  on upload, in the backups). Unlike body photos they are public: anyone may view a product photo.

## Alternatives considered

- **A ready-made shop builder on its own address.** Fastest to start and no code. Rejected by the
  developer: the shop belongs on the gym's own site; it would also keep a second customer account
  and a monthly fee.
- **The shop admin as a site account with a shop permission.** No new staff role. Rejected: it mixes
  the two sign-ins ADR 0010 keeps apart, and whoever receives the admin's SMS code would hold the
  shop's orders and customers' addresses.
- **The shop admin as an ordinary `Staff` user.** No new role at all. Rejected: Staff see every
  member, the till and the money, and the Owner wants the shop admin to see none of it.
- **A separate admin app on its own address for the shop.** Clean separation of the screens. Not
  chosen for now: a second login screen, a second host in Caddy and a third frontend entry, for
  what a role and a layout already give. Can be revisited if the shop grows a team of its own.
- **Customers with a shop account of their own, apart from site accounts.** A buyer would then
  never touch the gym's members. Rejected: a member who buys would need two accounts for one phone,
  and the *site users* rule keeps outside buyers out of the panel anyway.

## Consequences

- CLAUDE.md's "the panel, for 1 Owner and a few Staff" and BUSINESS_RULES.md §1's roles change once
  this is accepted: the panel also has shop admins, who see only the shop.
- **No staff policy may ever be "any signed-in staff user"** except the ones that must be (changing
  one's own password, signing out): such a policy would let a shop admin in. The `code-reviewer`
  checks it on every new endpoint.
- Online payment adds an outside party: a callback endpoint open to the internet, amounts in the
  gateway's unit (Rial) against the system's, and a way to find payments the gateway took but the
  system never confirmed.
- The public host's Caddy block must never proxy more of `/api` than the shop's path: a wider
  rule would put the staff's login on the gym's public address. `PublicHostShopTests` checks the
  Caddyfile.
- The eNamad needs pages the site does not have yet (about, contact, terms, returns), and the
  gateway goes in the name of whoever the eNamad is taken for.
- File storage, its disk space and its backup are needed from 16.3 (ADR 0010 planned them for
  progress photos, which are not built yet), and the image library ADR 0010 mentions is asked for
  in that task.
- The shop's SMS (order status, new message) go through the gym's Kavenegar account and are paid
  from it.
