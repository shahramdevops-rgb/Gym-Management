# The coloured dashboard (kept for Phase 13)

**Instruction for whoever does the UI update (Phase 13.2): do not redesign the dashboard. Bring
this design back from the git tag `dashboard-colour-v1` and apply it to the dashboard's content as
it is then.**

## Why it is not live

The dashboard was given colour, icons and a banner on 1405/07/14 (2026-10-06) and approved by the
developer. The same day the developer decided to hand the dashboard to the Owner in its first,
plain black-and-white look, and to keep the coloured design for the app-wide UI update instead of
throwing it away. The figures, cards and rules are the same in both looks; only the look differs.

## Where it is

- Git tag **`dashboard-colour-v1`** (commit `1d08ce2`), pushed to GitHub. Look at any file there
  with `git show dashboard-colour-v1:<path>`, or bring a file back with
  `git checkout dashboard-colour-v1 -- <path>`.
- The logo stays in the tree: `web/src/assets/brand/pasargad-logo.png` (white lines on
  transparency, cut from a photo of the gym's wall). Nothing imports it while the dashboard is
  plain.

## What the design is

- **Banner** (`components/DashboardHero.tsx`): a green gradient (`--hero-from`, `--hero-via`,
  `--hero-to`, light and dark values), rounded, about 160px tall. The title «داشبورد» and the
  range in words («گزارش از … تا …») at the start, the range presets as white pills on it, and the
  logo large at the far end (128px, 144px from `md`) on a soft white glow. The logo is hidden below
  `sm`.
- **Tones** (`tone.ts`, `index.css`): one accent per card, list and chart, as `--tone-*` tokens
  (blue, orange, green, violet, teal, amber, pink, sky, red, brown, indigo, lime, cyan), each with
  a dark-theme value. A component sets `--tone` through `toneStyle(tone)` and reads it with four
  utilities: `tone-soft` (14% tint), `tone-ink` (the tone mixed with the text colour),
  `tone-solid`, `tone-bar` (a bar fading in through transparency).
- **Stat cards** (`components/StatCard.tsx`): a 4px tone strip on top, an icon in a soft tinted
  square in the top corner, the figure larger and extra-bold, a lift on hover. The breakdown lines
  sit in the card's empty corner under the icon. A rate gets a thin progress bar. The change since
  the range before is a pill badge (green or red tint, with an arrow and a word). Skeleton cards
  pulse while the figures load (`StatCardsLoading`).
- **Section headings** (`components/SectionHeading.tsx`): an icon in a tinted square, the title,
  and an optional note at the end.
- **Receivables** (`components/ReceivablesCard.tsx`): one card with the total and a bar split by
  age (0–7, 8–30, over 30 days) instead of four cards.
- **Needs attention** (`components/NeedsAttentionPanel.tsx`): a coloured edge per list, a count
  badge, the person's initial in a tinted circle (`components/Initial.tsx`), an overdue cheque
  tinted red.
- **Charts** (`ChartCard`, `BarListChart`, `MonthlyChart`, `RevenueChart`, `AttendanceHeatmap`,
  `chartStyle.ts`): an icon per chart card, rounded gradient bars on a track (`svgId` makes the
  SVG gradient ids safe).
- **Staff table** (`components/StaffMoneyTable.tsx`): each name with its initial.

Each card's tone and icon, as the page used them:

| Card | Tone | Icon |
| --- | --- | --- |
| فروش (به غیر از آنالیز و فروشگاه) | violet | ShoppingBag |
| دریافتی | blue | Wallet |
| دریافتی آنالیز و فروشگاه | amber | Store |
| دریافتی بوفه | brown | CupSoda |
| دریافتی هوازی | pink | HeartPulse |
| دریافتی پلن | indigo | IdCard |
| دریافتی تک‌جلسه‌ای | lime | Ticket |
| هزینه‌ها | orange | Receipt |
| سود خالص | green | TrendingUp |
| ورود اعضا | sky | Footprints |
| اعضای جدید | pink | UserPlus |
| نرخ تمدید | teal | Repeat |
| نرخ تبدیل تک‌جلسه‌ای به پلن | cyan | ArrowLeftRight |
| پلن فعال / فریز / رو به پایان / کم‌جلسه | green / sky / amber / orange | CircleCheck / Snowflake / Hourglass / BatteryLow |
| Sections: خلاصهٔ بازه / امروز / نمودارها و جزئیات بازه | blue / green / violet | ChartColumn / CalendarCheck / Activity |

## How to bring it back

1. Restore the colour-only files from the tag: `DashboardHero.tsx`, `SectionHeading.tsx`,
   `ReceivablesCard.tsx`, `Initial.tsx`, `tone.ts`, and the tone and hero tokens and utilities in
   `index.css`.
2. Restore the chart, list and picker components from the tag, then check each against the
   current one for content changes made after 2026-10-06 (`git log dashboard-colour-v1..HEAD --
   web/src/features/dashboard`).
3. Merge `StatCard.tsx` and `DashboardPage.tsx` by hand: take the look from the tag and the cards,
   figures and texts from the current file. Cards added since then need a tone and an icon in the
   same spirit.
4. Rules that hold in both looks and must survive: a profit green and a loss red with its minus
   sign (BUSINESS_RULES.md §12 *Dashboard*); a change is an arrow, a word and a colour, never the
   colour alone; decoration is `aria-hidden`; every colour is a token with a dark value.
