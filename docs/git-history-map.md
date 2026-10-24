# Git history map (1405/08/03, 2026-10-25)

On 2026-10-25 the history of `main` was rewritten once, asked by the developer, to take Claude off
GitHub's list of contributors. Only commit messages changed: the `Co-Authored-By: Claude …
<noreply@anthropic.com>` line (189 commits) and the `Claude-Session:` line (14 commits) were removed.
Every commit's files, author, committer and dates are the same as before, checked commit by commit
(the tree of each of the 261 commits is unchanged); the tag `dashboard-colour-v1` was moved to its
rewritten commit and keeps its tagger and message.

Because a commit's id is a hash of its message too, every id changed. The documents written before
the rewrite (ROADMAP, LEARNING, docs/design, the release records) name the **old** ids; find the new
one here. Release names on the server (for example `20261025-0043-0d08196`) end with an old id and
are kept as they are: they are only names of images, and `./server.sh rollback` uses them by name.

The old history is kept in `D:\gymbackups\gym-git-before-rewrite-20261025.bundle`
(`git clone <bundle>` brings it back).

| Old | New | Date | Subject |
|---|---|---|---|
| `e8ec47c` | `b4cec77` | 2026-10-25 | docs(roadmap): close the panel on 1405/08/03; the public site is next |
| `9cd2e57` | `763775d` | 2026-10-25 | docs(roadmap): record the release 20261025-0043-0d08196 (13.1, 13.2, 14.5) |
| `0d08196` | `62c9497` | 2026-10-24 | feat(site): keep the panel out of search results and list the public page (14.5) |
| `1a7f3c6` | `8c18673` | 2026-10-23 | merge(web): bring main's SMS work (10.7 to 10.18) into the new look |
| `6ca61ab` | `9a1e205` | 2026-10-23 | docs(roadmap): record the SMS release 20261023-2305-6bcb031 (10.16 to 10.18) |
| `6bcb031` | `6026b5b` | 2026-10-23 | feat(sms): new wording for the absence, welcome and birthday SMS; dashboard on a month's 1st (10.18) |
| `a1df196` | `5a129b4` | 2026-10-23 | feat(sms): daily report time, sessions left in the expiring SMS, low-sessions rows by plan size (10.17) |
| `e25e86a` | `85e66f5` | 2026-10-23 | feat(sms): resend what Kavenegar cancelled, automatically and by hand (10.16) |
| `7e3ec22` | `21cea69` | 2026-10-20 | docs(roadmap): server restarted after the SMS release; 19 upgrades with Docker left for a night of their own |
| `1f84525` | `752e42c` | 2026-10-20 | docs(roadmap): SMS is live on the server with Kavenegar, every kind still off (10.6) |
| `33d177c` | `7b64ffc` | 2026-10-20 | docs(roadmap): record the SMS release 20261020-0246-099e551 |
| `099e551` | `e992e32` | 2026-10-20 | docs(screenshots): retake the README pictures before the SMS release |
| `7045d0f` | `f8c9e17` | 2026-10-20 | feat(sms): SMS goes live: the menu no longer marks its pages «آزمایشی» (10.6) |
| `29db953` | `be7f345` | 2026-10-20 | feat(sms): each kind on the settings page as a compact card with the exact SMS it sends, the broadcast as one of them (10.15) |
| `686abfd` | `3a8bc9a` | 2026-10-19 | feat(sms): show the SMS credit in the Owner's header, coloured by how much is left and flashing below 1,000 Toman (10.14) |
| `bd072d5` | `f52cd40` | 2026-10-19 | feat(sms): the Owner writes a broadcast to all active members or those with a plan, sent at once in the background (10.13) |
| `6f0fd48` | `e00e065` | 2026-10-19 | feat(sms): text a member with a usable plan who has not come for N days, once per absence (10.12) |
| `f7e322e` | `2378757` | 2026-10-19 | feat(sms): remind a member of a debt owed for N days, once per debt, with the whole open account (10.11) |
| `da6294f` | `d105e43` | 2026-10-19 | feat(sms): welcome a member by SMS when their first plan is sold, at once or at 10:00 after hours (10.10) |
| `fdad702` | `428bb49` | 2026-10-19 | feat(sms): send the member a receipt when a plan is fully paid, at once or at 10:00 after hours (10.9) |
| `221d734` | `c4160ee` | 2026-10-19 | fix(sms): birthday text gives the month and day without the year and ends «پیشاپیش تولدتان مبارک» (10.8) |
| `525eb84` | `71320f5` | 2026-10-19 | feat(sms): free first visit on the birthday and one birthday text sent 1–7 days ahead to members holding a plan (10.8) |
| `3c6e0c2` | `6908991` | 2026-10-19 | feat(sms): send the Owner a daily report at 22:45 with the day's check-ins, sales and takings (10.7) |
| `7f10bb9` | `0fefdf4` | 2026-10-18 | fix(demo): a member frozen at 13:00 skips the visit planned for that evening, so the demo keeps its frozen plan on every day |
| `806053a` | `427bdd1` | 2026-10-18 | docs(roadmap): release order, SMS-only next; the public site as Phase 14; 6.5.37's pre-release step settled |
| `a45a11e` | `7ae6665` | 2026-10-18 | feat(sms): end every text with Kavenegar's opt-out line; plan seven new SMS kinds (10.4, 10.7–10.13) |
| `9e7ac72` | `84adcce` | 2026-10-16 | docs(roadmap): record the releases 20261016-1717-7ed7006 and 20261016-1747-c6ff9ec |
| `c6ff9ec` | `0c8b460` | 2026-10-16 | fix(deploy): the public site's photos are revalidated and their addresses carry a fingerprint, so a release is seen at once (13.3) |
| `7ed7006` | `d1d623e` | 2026-10-16 | docs(screenshots): retake the README pictures before the release |
| `7a53bac` | `d04bc0f` | 2026-10-16 | feat(public-site): calisthenics on the plan, the tour and the hero; a narrower phone menu (13.3) |
| `79dc23d` | `61c458c` | 2026-10-16 | feat(public-site): six faceless hero slides, the name «پاسارگاد پلاس» without «جیم», every room named on the phone's plan (13.3) |
| `5fefd94` | `d6c251b` | 2026-10-16 | fix(public-site): the phone's menu drawer, tall photos, bigger trainer cards, the whole plan, and bronze icons (13.3) |
| `ea542f0` | `0e3ea20` | 2026-10-15 | docs(roadmap): record the release 20261015-1423-4f9347d |
| `4f9347d` | `768c505` | 2026-10-15 | docs(screenshots): retake the README pictures before the public site release |
| `23570c2` | `4c2185a` | 2026-10-15 | fix(deploy): the public page, its stylesheet and script are revalidated, never served stale (13.3) |
| `35a82a2` | `f7bdfed` | 2026-10-15 | feat(public-site): the figure stage among the opening line's points (13.3) |
| `78b8e13` | `dc5b8a6` | 2026-10-15 | feat(public-site): the four points with moving signs, and the figure stage on the plan (13.3) |
| `9f041e3` | `22df78c` | 2026-10-15 | feat(public-site): the numbers on barbell plates, and a thinner glass under the plan (13.3) |
| `77c191e` | `8c213ee` | 2026-10-15 | feat(public-site): one section for the facilities and the tour, and a new opening line (13.3) |
| `1d5b569` | `af480e8` | 2026-10-15 | feat(public-site): add the shop showcase and drop the "no columns" lines (13.3) |
| `c4b0c56` | `0429579` | 2026-10-15 | feat(public-site): add the trainers' stage, cards that tilt and turn over (13.3) |
| `077105a` | `ca175fd` | 2026-10-15 | feat(public-site): add the sign-up button and queue trainers, shop and member accounts (13.3) |
| `0abdc41` | `7dfeff0` | 2026-10-15 | feat(public-site): walk through the hall on scroll, with the gym's plan, contact details and photos (13.3) |
| `e6bdd98` | `a2e014e` | 2026-10-14 | feat(public-site): build the first page in the "stone and steel" direction (13.3) |
| `66da39b` | `e5435ae` | 2026-10-14 | fix(sms): the settings page says messages go from the dedicated line, not templates |
| `6d1abf7` | `ea7a838` | 2026-10-14 | Merge branch 'main' into task/13.2-visual-language |
| `f70d86c` | `3f0df58` | 2026-10-14 | feat(subscriptions): cancelling a subscription gives the money back, no refund by hand (6.5.37) |
| `dac9824` | `4983c0b` | 2026-10-14 | feat(web): sessions as «used از total» again, solid tags everywhere, the locker search's phone on one line |
| `f58b966` | `923969b` | 2026-10-13 | feat(web): fixes from the UX review: last-session notice, check-in hints, double-click guard, remaining sessions everywhere |
| `9d87afb` | `a27d0ae` | 2026-10-13 | feat(web): Persian comma in date-time, «باشه» on the receipt, Persian digits while typing (13.1 follow-up) |
| `903d15e` | `082a9c2` | 2026-10-13 | feat(web): forms fill the phone's screen with their buttons in reach, the locker screen's phone fixes (13.1c) |
| `310941d` | `df3653e` | 2026-10-12 | feat(web): tables become cards on a phone, the members search and filters on one line (13.1b) |
| `9fb9291` | `810b1bc` | 2026-10-12 | feat(web): the frame and the desk on a phone, the computer untouched (13.1a) |
| `8a7d4ce` | `9537b60` | 2026-10-12 | feat(web): member profile in the panel's look, details and debt facing the subscription (13.2j) |
| `a0a8966` | `68a5c45` | 2026-10-12 | feat(web): menu line from the title's end, readable state text and full-tone edges on white (13.2j) |
| `9cfc87a` | `0de80ef` | 2026-10-12 | feat(web): finance pages in the panel's look, the dashboard on the slim banner (13.2i) |
| `0f7f1bd` | `8ddfa66` | 2026-10-12 | fix(web): keep each tone's hue in its edges and turn the cheque alert yellow (13.2i) |
| `d22255f` | `e0cf7df` | 2026-10-12 | feat(web): cafe pages in the panel's look, form buttons edged and banner tools at the far end (13.2i) |
| `d3b4df3` | `ad18dd5` | 2026-10-12 | feat(web): row edges, edged tags and buttons, filters on the page's colour on three pages (13.2i) |
| `76f0918` | `65cc838` | 2026-10-12 | fix(web): keep the hourly chart's hidden table from lengthening the locker page (13.2h) |
| `e9fd986` | `4e520dc` | 2026-10-12 | feat(web): frameless menu and lockers, one door-like edge for boxes, three pages on the banner (13.2h) |
| `7e98325` | `3989a26` | 2026-10-12 | feat(web): locker page banner, menu group lines and a slimmer header (13.2h) |
| `0ed5c83` | `346ff37` | 2026-10-11 | feat(web): sign-in page in the panel's look, page messages and the member profile header (13.2g) |
| `51d8d21` | `3fe916c` | 2026-10-11 | feat(web): totals as tiles, tinted empty states and a floating side menu (13.2f) |
| `e23d107` | `a7a2941` | 2026-10-11 | feat(web): colour on every page from its menu group, violet dashboard banner (13.2e) |
| `d03c183` | `15f7d44` | 2026-10-11 | docs(roadmap): plan 13.2e, colour on every page |
| `87c79a8` | `c5a6866` | 2026-10-11 | feat(web): Persian figures everywhere, page states and new screenshots (13.2d) |
| `f8d2b62` | `86f4714` | 2026-10-11 | feat(web): every edit form saves the way SMS settings does (13.2c) |
| `f5ae899` | `4303e16` | 2026-10-11 | feat(web): bring back the coloured dashboard from dashboard-colour-v1 (13.2b) |
| `e7eda23` | `affd006` | 2026-10-11 | feat(web): one visual language — tokens, app frame and shared page components (13.2a) |
| `be22691` | `df403e4` | 2026-10-11 | feat(demo): add admin seed-demo, README screenshots, ADRs 0005-0009 and the deployment guide |
| `6b782ed` | `fe47f6c` | 2026-10-11 | docs(roadmap): record the release 20261010-2339-7ebfb0d |
| `7ebfb0d` | `1f910f7` | 2026-10-10 | feat(web): mark the members, audit and SMS pages in the menu after the 18 Mehr release |
| `596ed5c` | `94fe7b8` | 2026-10-10 | docs(roadmap): record the release 20261010-2323-3428fa7 |
| `3428fa7` | `1794e5a` | 2026-10-10 | docs(sms): record the dedicated line and that blocked members still get it |
| `e27a288` | `de8ec4c` | 2026-10-10 | docs(attendance): drop the opening-hours rule (task 11.4) |
| `86e292a` | `3cc7669` | 2026-10-10 | feat(performance): index and foreign key on payments.cafe_order_id, JIT off, log review fixes and front desk e2e |
| `2958c9d` | `d4b0908` | 2026-10-10 | feat(security): add a content security policy and rate-limit refresh and logout |
| `69f96d3` | `3f1c593` | 2026-10-10 | feat(audit): add the audit screen «گزارش تغییرات» for the Owner |
| `d05e4f2` | `6da8c36` | 2026-10-09 | feat(sms): refund blocked and cancelled messages, retry 451, name Kavenegar's codes |
| `855d070` | `e7bc415` | 2026-10-09 | docs(sms): record the first Kavenegar test and that the line is not bought yet |
| `26b889d` | `47d7219` | 2026-10-09 | feat(sms): send every SMS as free text from the gym's dedicated line |
| `c21d53e` | `4d60293` | 2026-10-08 | feat(sms): add the SMS history page and resend |
| `1695226` | `50bc4bb` | 2026-10-08 | feat(sms): say beside «ذخیره» whether the settings were saved or unchanged |
| `084a3d3` | `1744ccc` | 2026-10-08 | docs(roadmap): record the Kavenegar credit check with the developer's key |
| `42f6a11` | `5762942` | 2026-10-07 | docs(roadmap): plan absence SMS, birthday free entry and discount codes |
| `5a73a0c` | `22d1876` | 2026-10-07 | feat(sms): add the Kavenegar sender, kept from sending outside Production |
| `7a9c067` | `d763110` | 2026-10-07 | feat(sales): add «متفرقه», a price-only sale at the desk |
| `f20eec9` | `d6a514c` | 2026-10-07 | fix(attendance): let anyone in for cardio only, with or without a plan |
| `6242691` | `4999b2b` | 2026-10-07 | feat(members): list by latest visit, with tags and a sessions column |
| `4cb89f8` | `b81121a` | 2026-10-06 | feat(sms): daily runs for the four kinds at their send times |
| `ebcdb04` | `d31707b` | 2026-10-06 | feat(sms): settings page with on/off, numbers, send times and templates |
| `8c5d91e` | `f732e49` | 2026-10-06 | feat(sms): notification model, template-first sender and the fake provider |
| `99c8335` | `19375df` | 2026-10-06 | docs(sms): decide phase 10 rules and split it into six tasks |
| `4206c06` | `84c25b6` | 2026-10-06 | docs(roadmap): record the release 20261006-0505-73e6c33 |
| `73e6c33` | `bd687d4` | 2026-10-06 | feat(web): mark the dashboard and cheques «جدید» in the menu through 15 Mehr |
| `f9a5747` | `6a26aa3` | 2026-10-06 | style(dashboard): back to the plain look for the handover, the coloured design kept under a tag |
| `1d08ce2` | `2147b8c` | 2026-10-06 | feat(dashboard): cafe and cardio received cards, plan cards renamed, cafe profit removed |
| `4cfe648` | `2df5dd4` | 2026-10-06 | feat(dashboard): plans and single visits paid and owed, the renewal rate in plain words |
| `ebbb055` | `afcdfc0` | 2026-10-06 | feat(dashboard): received money for the nightly drawer check, sales paid and owed |
| `aff716a` | `fb4d9c7` | 2026-10-06 | feat(dashboard): sales first, with what was paid on them by method |
| `f3d15c1` | `5ac4064` | 2026-10-06 | feat(dashboard): plan and single-visit cards, single visit to plan conversion rate |
| `7fd9fbc` | `125dcd1` | 2026-10-06 | style(dashboard): a shorter banner with a wider logo |
| `17c555b` | `57146fa` | 2026-10-06 | feat(dashboard): net profit without shop and analysis, clearer card names |
| `22e1f82` | `1db4e5d` | 2026-10-06 | feat(dashboard): green banner with the gym's logo, sales without shop and analysis |
| `7feeca9` | `5faaf94` | 2026-10-06 | feat(dashboard): show a loss with its minus sign, profit green and loss red |
| `288611d` | `2b474e5` | 2026-10-06 | style(dashboard): give the dashboard colour, icons and a banner |
| `cda34c2` | `88e416a` | 2026-10-06 | feat(payables): header alert for cheques and instalments due within 5 days |
| `0e2f070` | `c24777c` | 2026-10-05 | feat(dates): date filters keep the typed box with a calendar |
| `45ee501` | `7a83334` | 2026-10-05 | style(web): apply prettier to 13 files that missed it |
| `631370c` | `6e1ac91` | 2026-10-05 | feat(dates): choose every date in day/month/year dropdowns like the birth date |
| `be0e8ec` | `a254f8f` | 2026-10-05 | docs(roadmap): drop 9.5 shift handover, staff log in with their own accounts |
| `b8e7637` | `b4b8384` | 2026-10-05 | feat(dashboard): plans and single visits sold on the revenue-by-source chart (9.3) |
| `16e7293` | `0e069a0` | 2026-10-05 | feat(payables): cheques and instalments recorded as expenses when paid (9.4) |
| `c3c22a1` | `487a9af` | 2026-10-05 | feat(cheques): cheque register and the dashboard's cheques-coming-due reminder (9.4) |
| `23e6b0b` | `3649901` | 2026-10-04 | feat(dashboard): Owner's dashboard with Jalali range presets, summary cards and charts (9.3) |
| `1a3288a` | `52552b6` | 2026-10-04 | feat(reports): operational reports and needs-attention lists for the Owner (9.2) |
| `1d3b5a1` | `2af23b0` | 2026-10-04 | feat(reports): financial report and receivables API for the Owner (9.1) |
| `b5fa7d4` | `08f01ba` | 2026-10-04 | docs(roadmap): record the housekeeping before Phase 9 |
| `93c704f` | `1539064` | 2026-10-04 | docs(roadmap): mark 6.5.28 to 6.5.32 as released in 20261004-0511-2793a16 |
| `6b3159e` | `a879084` | 2026-10-04 | feat(guests): remove the guest debt list; auto-checkout keeps an owing guest inside (6.5.33) |
| `01e04fe` | `a3e3992` | 2026-10-04 | docs(roadmap): record the 20261004-0511-2793a16 release over real data |
| `2793a16` | `cfce5a2` | 2026-10-04 | feat(members): choose the birth date with day/month/year dropdowns |
| `3777497` | `5d5c478` | 2026-10-04 | feat(guests): every service for guests under the cafe rule, and the guest debt list (6.5.31) |
| `f0fa3e4` | `1a45743` | 2026-10-04 | feat(history): totals under the sales and payments sections for the Owner (6.5.32) |
| `a096a94` | `d76cca5` | 2026-10-04 | feat(history): sales sections with a paid/unpaid filter for the Owner (6.5.30) |
| `0de61dc` | `ee59e78` | 2026-10-04 | fix(lockers): keep the sessions beside a long member name in the visit dialog |
| `abfc071` | `e75876a` | 2026-10-04 | feat(service-charges): purchase tiles, multi-item shop sale and price-only analysis on account (6.5.29) |
| `43d807a` | `2ee1f85` | 2026-10-03 | feat(service-charges): miscellaneous sale from the locker box (6.5.28) |
| `dfb7f29` | `ffb4be8` | 2026-10-03 | feat(attendance): cardio-only visit with no session consumed and check-out gated on the cardio amount (6.5.27) |
| `06d6ccc` | `b819aa5` | 2026-10-03 | feat(payments): keep a settlement's rows together in the payment history (6.5.26) |
| `02b8ddf` | `4e3c9f3` | 2026-10-03 | feat(history): list guests in the gym's history (6.5.25) |
| `84831ad` | `514df77` | 2026-10-03 | feat(attendance): guest visits with their own locker colour and one-step cafe settlement |
| `5e15db7` | `de6acc0` | 2026-10-02 | feat(history): add the gym's history page (6.5.25) |
| `f35b16d` | `1b9b485` | 2026-10-02 | docs(roadmap): record the go-live release and the real-data rules |
| `3059c81` | `62d9932` | 2026-10-02 | docs: the server holds real data from the go-live wipe on 2026-10-02 |
| `44c6b9f` | `aa1d494` | 2026-10-02 | docs(roadmap): plan the gym history page (6.5.25) |
| `acd8f24` | `97a98ee` | 2026-10-02 | chore(dev): move the local https port from 7134 to 5135 |
| `a2c991c` | `e6438d6` | 2026-10-02 | fix(lockers): show the holder's name on smaller doors |
| `18d22af` | `d1271ea` | 2026-10-02 | feat(members): birth date required; the date box takes typing as well as the calendar |
| `d7a7463` | `39f392d` | 2026-10-02 | fix(web): Jalali calendar opens in place under the date field |
| `d1dfa04` | `c18295e` | 2026-10-02 | feat(subscriptions): queued plans read as provisional; history shows the sale date |
| `0dc7f27` | `53220ad` | 2026-10-01 | feat(web): dark theme switch in the header, remembered per device |
| `f383933` | `b18af83` | 2026-10-01 | feat(web): one light theme; the locker map no longer turns the app dark |
| `259b79c` | `e45a3df` | 2026-09-30 | feat(members): debtors filter on the member list |
| `18ad74c` | `dbec260` | 2026-09-29 | feat(members): search and register on the member list; roomier occupied-locker box |
| `4cafeb6` | `7bd51d9` | 2026-09-29 | docs(deploy): record the 0514e28 release and the wipe step for trial data |
| `0514e28` | `2ed0924` | 2026-09-29 | feat(lockers): locker usage map over the last 7, 30 or 90 days |
| `86bcc79` | `73949de` | 2026-09-29 | feat(attendance): today's check-ins by hour under the locker map |
| `7fc3602` | `df444c6` | 2026-09-29 | fix(attendance): defer the overlap check at check-in; readable select options in dark mode |
| `8bf7709` | `bae9dd0` | 2026-09-29 | feat(subscriptions): derive plan days from the session count |
| `8799640` | `68177fb` | 2026-09-29 | feat(lockers): dark desk screen, name search and a birthday door party |
| `8ca7906` | `88a99d8` | 2026-09-29 | feat(lockers): fill a bar along an occupied door over a long stay |
| `7d0e653` | `38225bf` | 2026-09-29 | feat(lockers): desk panel with birthdays and renewal opportunities |
| `9dfd3d6` | `112c85f` | 2026-09-29 | docs(attendance): write the guest visit rules and task 6.5.11 |
| `6022d76` | `7faab70` | 2026-09-29 | fix(ui): close the desk dialogs on a click outside |
| `f15b087` | `04361fd` | 2026-09-29 | feat(lockers): mark a debtor's door "بدهکار" on the locker map |
| `85fd5c2` | `6310a9a` | 2026-09-29 | fix(cafe): refresh the member's debt in the locker box so till orders reach the settlement |
| `2185730` | `1c566ba` | 2026-09-28 | feat(cafe): tie till orders to the open visit and buy only from the locker |
| `d06a2f1` | `3d3355d` | 2026-09-28 | fix(lockers): show today's history only before a member is chosen |
| `a0e171f` | `7aca61b` | 2026-09-28 | feat(members): list cafe items with prices under the cafe row of the debt breakdown |
| `3d739b6` | `d4ce0fc` | 2026-09-28 | feat(lockers): show who had a locker today from the check-in box |
| `9e58482` | `ebcf842` | 2026-09-28 | feat(lockers): bigger doors that show the holder's name |
| `ade404e` | `da43770` | 2026-09-28 | feat(members): show a frozen badge next to the member's status on the list |
| `0841154` | `a92ad1d` | 2026-09-28 | feat(subscriptions): confirm before freezing or unfreezing, showing the 30-day cap |
| `b3703f6` | `1729f4c` | 2026-09-28 | feat(attendance): check-in unfreezes a frozen plan |
| `a6a47d0` | `2d6e21f` | 2026-09-28 | feat(attendance): cancel check-in asks per purchase whether to cancel cardio and cafe |
| `55e4fa8` | `b1a051b` | 2026-09-28 | feat(attendance): sell a plan or single visit at the locker and check in at once |
| `8d9b0c2` | `4434280` | 2026-09-28 | feat(subscriptions): custom plans priced per session, prices set in settings |
| `287e206` | `de73479` | 2026-09-27 | feat(lockers): locker map where the desk picks the locker |
| `96a4ac2` | `49259e9` | 2026-09-27 | docs(lockers): plan 6.5.5 - fixed lockers, the desk picks the locker |
| `7644b75` | `46e0c2f` | 2026-09-27 | feat(expenses): expenses screen with filters, total, edit, void and categories |
| `519b6d3` | `d6ece2e` | 2026-09-27 | chore(web): gen:api starts and stops the API itself |
| `db7efd7` | `86c82b4` | 2026-09-27 | feat(expenses): expenses API with seeded categories, edit and void |
| `615bed1` | `a63cf38` | 2026-09-27 | docs(roadmap): close 11.6 - released, and the Owner renamed on the server |
| `9eb8f63` | `26574d7` | 2026-09-27 | fix(auth): refuse the gym's own name anywhere in a password |
| `71e864c` | `b0e736c` | 2026-09-27 | fix(auth): put the password eye button on the right of the English text |
| `617941f` | `868288e` | 2026-09-27 | fix(api): do not log the host abort that dotnet ef causes as fatal |
| `f866b90` | `9022c7b` | 2026-09-27 | Merge branch 'task/11.5-password-policy-and-lockout' |
| `98bedc1` | `71c71be` | 2026-09-26 | feat(auth): trusted-device lockout, owner unlock and server account commands |
| `13167ce` | `440640d` | 2026-09-26 | feat(auth): NIST password policy with a blocklist and a live checklist |
| `72dc8f9` | `33eca5d` | 2026-09-26 | chore(attendance): move closing time to midnight |
| `44d461a` | `0389ee4` | 2026-09-26 | feat(payments): require a chosen method and confirm the money before every payment |
| `ae53c0f` | `2d2064e` | 2026-09-26 | feat(payments): settle several owed items with one amount (تسویه یکجا) |
| `08272d4` | `0a8640c` | 2026-09-26 | feat(attendance): confirm saved cafe purchases and cardio amounts in the dialog |
| `81c307b` | `8303140` | 2026-09-26 | feat(attendance): confirm before cancelling a check-in |
| `6f12d84` | `04cea3a` | 2026-09-26 | docs(cafe): cafe orders stay when a check-in is cancelled |
| `4e07f81` | `50026f6` | 2026-09-26 | feat(cafe): take cafe purchases on the currently-inside board and list them at check-out |
| `92a79bb` | `32cd6f4` | 2026-09-26 | feat(cafe): till, menu management and order history screens |
| `993e3f7` | `27e4c69` | 2026-09-26 | feat(attendance): confirm check-in and check-out on every screen, and take the key back first |
| `8441b9b` | `9752f37` | 2026-09-26 | feat(cafe): cancel an order with refunds, and list order history |
| `ebfe121` | `f1109e1` | 2026-09-26 | feat(attendance): confirm check-in and check-out in one dialog at the front desk |
| `9dae41d` | `44260e6` | 2026-09-26 | docs(attendance): record opening hours as a pending rule |
| `3856860` | `9ef4500` | 2026-09-26 | chore(claude): add a Haiku test-runner agent and a cheaper commit routine |
| `6b89fa4` | `ccc56aa` | 2026-09-26 | feat(cafe): a price list the front desk owns, and orders on account |
| `79044c1` | `7470863` | 2026-09-26 | feat(attendance): finish the entry screen for walk-in visitors |
| `8fa9e28` | `1416577` | 2026-09-26 | feat(subscriptions): sell a single visit as an ordinary subscription |
| `88bdd96` | `cacad55` | 2026-09-25 | docs(subscriptions): decide how a single visit is sold and entered |
| `89ccbd0` | `636c620` | 2026-09-25 | docs(roadmap): close 6.5.0 — the panel is on its own host |
| `527ecf7` | `5effdca` | 2026-09-25 | feat(deploy): give the panel its own host and the gym a public page |
| `c4e8a36` | `a98429c` | 2026-09-25 | feat(attendance): keep every board row to one line and show what is running out |
| `42cd289` | `01d6def` | 2026-09-25 | feat(lockers): let the front desk see and service lockers |
| `80edf85` | `be1f405` | 2026-09-25 | docs(deploy): close task 6.4 — the system is live |
| `e8b28e9` | `b3b571d` | 2026-09-25 | docs(roadmap): record what the first real use of the system turned up |
| `41c584e` | `cd19ae6` | 2026-09-25 | chore(backup): keep 120 nightly dumps instead of 60 |
| `fd35eb8` | `33022a9` | 2026-09-24 | docs(deploy): add a commands-only quick reference for server setup |
| `adf4519` | `6916380` | 2026-09-24 | docs(roadmap): record the DNS cause and the nameserver fix |
| `b7d33a2` | `3706591` | 2026-09-24 | feat(deploy): prepare the production server and redirect the second domain |
| `df17d18` | `03b879e` | 2026-09-24 | fix(deploy): make the release and restore safety nets actually fire |
| `e130951` | `57610e1` | 2026-09-24 | feat(backup): nightly dump, restore script and Windows pull script |
| `e5b6fe9` | `407a8c9` | 2026-09-24 | feat(deploy): release scripts, migration bundle service and forwarded headers |
| `693a526` | `1c5dad9` | 2026-09-24 | feat(deploy): production images, compose stack and Caddy config |
| `22aec97` | `416d7ff` | 2026-09-23 | feat(service-charges): charge هوازی to a visit and settle it like anything else |
| `c72a329` | `297d0f0` | 2026-09-23 | feat(web): one money field for every typed amount |
| `5655ef4` | `0c23631` | 2026-09-23 | feat(members): show what a member owes and guard cancel and refund |
| `3bfaddc` | `848f90a` | 2026-09-23 | feat(members): add birth date with the first Jalali date input |
| `3d07952` | `03fe52d` | 2026-09-22 | docs(rules): record open accounts, birth date, cardio and the money field |
| `6b6a0c0` | `4791d11` | 2026-09-22 | fix(attendance): say the next subscription starts tomorrow |
| `5f66544` | `1cb046d` | 2026-09-22 | feat(health): report a database that is missing migrations |
| `11fa4c0` | `4fcc919` | 2026-09-22 | feat(subscriptions): read the plan name live instead of snapshotting it |
| `f0f72d1` | `d0b9ab7` | 2026-09-22 | fix(attendance): check in against the subscription in effect today |
| `cb93ca1` | `a68f9be` | 2026-09-22 | feat(attendance): assign a random free locker instead of the lowest number |
| `e37060b` | `0d73f5b` | 2026-09-22 | docs(roadmap): record that the payments branch is now merged |
| `51b93aa` | `c973490` | 2026-09-22 | Merge branch 'task/4.4-payments' into task/5.6-front-desk-ui |
| `3963c4a` | `8b4c28c` | 2026-09-22 | docs(deployment): rewrite phase 6 for a rented Iranian VPS |
| `2b09a92` | `a8616e5` | 2026-09-22 | feat(web): add front desk check-in, check-out and lockers screens |
| `05d6db2` | `0deb3b6` | 2026-09-20 | feat(attendance): add Hangfire and the nightly auto-checkout job |
| `e28787a` | `0efc3f4` | 2026-09-20 | test(attendance): add concurrency tests for check-in races |
| `41881ca` | `9302b55` | 2026-09-20 | feat(attendance): add check-out, cancel check-in, and front desk lists |
| `f97fe0a` | `7c8d689` | 2026-09-20 | feat(attendance): add check-in with locker assignment and session consumption |
| `ffa8ed0` | `9454df5` | 2026-09-20 | feat(lockers): add locker entity and owner setup endpoints |
| `116ee65` | `a117d76` | 2026-09-20 | feat(payments): complete UI for subscriptions and payments (task 4.6) |
| `66a8f85` | `2b1c791` | 2026-09-19 | feat(payments): add refunds, member subscription history, and member payment history |
| `6a63b24` | `808c3a7` | 2026-09-19 | feat(payments): add payment entity, register payment endpoint, and calculated payment status |
| `ae09fec` | `b409d3d` | 2026-09-19 | feat(subscriptions): add freeze, unfreeze, and cancel endpoints |
| `9ec0002` | `98d999d` | 2026-09-19 | completation of the section 4 |
| `d089e1d` | `7635f4d` | 2026-09-19 | feat(subscriptions): add subscription domain model with snapshot, calculated status, sessions, freeze and cancel |
| `187f250` | `95b8fde` | 2026-09-19 | feat(web): add owner plans screen with list, create, edit and activate/deactivate |
| `c631e13` | `e0bc69f` | 2026-09-18 | feat(plans): add plan entity, owner-only create/update/activate/deactivate and list endpoints |
| `4a5f15f` | `9aa278a` | 2026-09-18 | feat(web): add member search, list, create/edit forms and profile page |
| `4b16d4d` | `d9ffcec` | 2026-09-18 | feat(members): add member get, paged search by name or phone, and deactivate/reactivate |
| `ed41b20` | `2231fd7` | 2026-09-18 | feat(members): add member entity, phone and Persian text normalization, create and update endpoints |
| `9e17469` | `0c23259` | 2026-09-18 | fix(auth): harden phase 1 against login races, cross-tab refresh and user switching |
| `1255e40` | `7c9c994` | 2026-09-18 | feat(web): add login, silent refresh, forced password change and staff management UI |
| `922bbbe` | `c4efa80` | 2026-09-18 | feat(audit): add append-only audit log written by a save interceptor |
| `cf85c35` | `2c455ad` | 2026-09-18 | feat(staff): add owner staff management API with deactivation and password reset |
| `0cccb73` | `baebec3` | 2026-09-18 | feat(auth): add current user, role policies, password change gate and change-password endpoint |
| `21e3221` | `4ae0074` | 2026-09-18 | feat(auth): add refresh token rotation with reuse detection and logout |
| `f66a003` | `ed89b83` | 2026-09-18 | feat(auth): add login endpoint with JWT access tokens, lockout and rate limiting |
| `02e9ecc` | `ba589d2` | 2026-09-18 | feat(identity): add Identity user, roles and idempotent owner seeding |
| `8ecac00` | `b432166` | 2026-09-18 | docs: record task 0.8 learning notes and tick roadmap |
| `90f98fb` | `7b23a68` | 2026-09-18 | ci: add GitHub Actions workflow, ADR 0001 and README |
| `3287176` | `b6311d9` | 2026-09-18 | feat(web): add Persian RTL frontend skeleton with health status page |
| `c732b4c` | `2c711ef` | 2026-09-18 | test: add integration harness with Testcontainers and Respawn |
| `c5c7618` | `b8da33c` | 2026-09-18 | feat(api): add Result, error mapping and request validation |
| `7a433a9` | `4094896` | 2026-09-18 | chore: stop tracking local Claude Code tool permissions |
| `5a67fd9` | `ef9f9bf` | 2026-09-18 | feat(api): add Serilog, OpenAPI, correlation id and CORS plumbing |
| `013996d` | `842753a` | 2026-09-18 | feat(persistence): add EF Core context, audit interceptor and health check |
| `f9252ec` | `6cab4a4` | 2026-09-18 | feat(infra): add local Postgres and Seq containers |
| `c34181b` | `784b708` | 2026-09-18 | chore: project skeleton with clean architecture references |
| `442cefb` | `277c2d9` | 2026-09-17 | chore: add project documentation, gitignore and gitattributes |
