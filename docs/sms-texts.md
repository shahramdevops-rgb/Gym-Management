# SMS texts

The wording of every SMS the gym sends (BUSINESS_RULES.md §10). Agreed with the developer on
1405/07/14 (2026-10-06), task 10.1.

The wording lives **in the code** (`SmsText` in `Gym.Domain/Notifications`), and the system sends
the whole text with Kavenegar's `sms/send` from the gym's dedicated line. These were first written
as Kavenegar templates, but Kavenegar refused every template (the gym's site was not up yet), so the
gym is buying a dedicated line, which needs none (decided 1405/07/16; not bought yet on
1405/07/17, since buying it needs identity verification and a business licence). Changing a text is a release: change it
here and in `SmsText` together, and the domain tests that hold each text.

## The values

Numbers and dates are written the way the app shows them: Persian digits, the Jalali date as
`۱۴۰۵/۰۷/۲۰`, an amount with thousands separators (`٬`) and no space.

A long name or payee is **simply cut short** (decided with the developer when these were templates,
and kept so one long name cannot make a message cost several parts): a member's name keeps its first
6 words and a payee its first 9, then the first 100 characters (`SmsText.Fit`).

**Length:** a Persian SMS holds 70 characters in one part and 67 in each part of a longer one; each
part is paid for. The lengths below take a 10-character name and a 20-character payee, and include
the opt-out line. The developer decided that length is not a concern for these four.

## The opt-out line

Every text ends with a line of its own holding **`لغو11`**: English digits, no space (decided with
the developer on 1405/07/26, 2026-10-18). Kavenegar adds it to every message from the gym's line
anyway, as the first messages showed; written here, the history holds what reached the phone and
Kavenegar does not add a second one. `SmsText.OptOut`. Messages written before keep their text.

## The texts

`{name}` and the other braces mark where a value goes. Each text below is followed by the line
`لغو11`, left out here.

### Subscription running out (`SubscriptionExpiring`, to the member)

```
{name} عزیز، اشتراک شما در باشگاه پاسارگاد {end date} به پایان می‌رسد.
```

- The end date and not "n days left", which would read «۰ روز دیگر» on the last day.
- About 80 characters: two parts.

### Few sessions left (`LowSessions`, to the member)

```
{name} عزیز، فقط {sessions left} جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.
```

- About 77 characters: two parts.

### Birthday (`Birthday`, to the member)

One text, chosen by the developer on 1405/07/27 (2026-10-19), task 10.8. It is always sent 1 to 7
days before the birthday, never on the day, and tells the member of the free visit
(BUSINESS_RULES.md §7 *Birthday visit*). It replaced two texts, one for the day itself and one
«پیشاپیش»; messages written before keep their text.

```
{name} عزیز، {month/day} روز تولد شماست؛ آن روز مهمان باشگاه پاسارگاد هستید و ورودتان رایگان است. پیشاپیش تولدتان مبارک!
```

- `{month/day}`: the birthday's Jalali month and day **without the year** (the developer,
  1405/07/27), e.g. `۰۷/۱۶` (`SmsText.MonthAndDay`, as in the daily report); `۱۲/۲۹` for someone
  born on 30 Esfand in a year without it.
- About 124 characters: two parts.

### Cheque or instalment coming due (`PayableDue`, to the Owner)

```
یادآوری {چک or قسط}: {amount} تومان، سررسید {date}، به {payee}
```

- `{date}`: the date written on the cheque or the instalment's due day (Jalali).
- `{amount}`: in Toman, e.g. `۱۲٬۵۰۰٬۰۰۰`.
- About 78 characters: two parts. One SMS for each cheque and each instalment.

### Daily report (`DailyReport`, to the Owner)

Agreed with the developer on 1405/07/27 (2026-10-19), task 10.7. Sent every night at 22:45 about
that same day (BUSINESS_RULES.md §10 *The daily report*).

```
گزارش باشگاه پاسارگاد {month/day}
ورود: {check-ins}
پلن: {plans sold}، تک‌جلسه: {single visits sold}
دریافتی: {received} هزار تومان
نقد: {cash}
کارت: {card}
انتقال: {bank transfer}
```

- `{month/day}`: the Jalali month and day without the year, e.g. `۰۷/۲۷` (`SmsText.MonthAndDay`).
- The amounts are **thousands of Toman**, each rounded on its own to the nearest thousand, 500 rounded
  away from zero (`SmsText.Thousands`): `۱۲٬۳۴۵٬۶۰۰` تومان is written `۱۲٬۳۴۶`. A figure below zero
  (more refunded than taken) carries the app's minus sign with a left-to-right mark before it, as
  `formatMoney` writes it.
- **The `نقد` line is left out on a day with no cash**; `کارت` and `انتقال` are always there.
- No SMS for a day with nothing (every figure zero).
- About 125 characters for a busy day: two parts. Written in full Toman it would be about 136, three
  parts; that is why the developer took the zeros off.

### Subscription receipt (`SubscriptionReceipt`, to the member)

Agreed with the developer on 1405/07/27 (2026-10-19), task 10.9. Sent when a plan becomes fully paid
(BUSINESS_RULES.md §10 *The subscription receipt*).

```
{name} عزیز، اشتراک {sessions} جلسه‌ای شما در باشگاه پاسارگاد از {start} تا {end} ثبت و {price} تومان کامل پرداخت شد. سپاس!
```

A plan queued behind another, whose dates are not settled yet (§4), has its days instead of dates:

```
{name} عزیز، اشتراک {sessions} جلسه‌ای شما در باشگاه پاسارگاد ({days} روز، پس از اشتراک فعلی) ثبت و {price} تومان کامل پرداخت شد. سپاس!
```

- `{start}` and `{end}`: the Jalali month and day **without the year** (the developer chose it), e.g.
  `۰۷/۲۷` (`SmsText.MonthAndDay`).
- `{price}`: the subscription's price in Toman, e.g. `۱٬۲۰۰٬۰۰۰`.
- About 125 characters with a 10-character name, two parts; about 135 for a queued plan, three. The
  developer took «مبلغ» and «پایان» out to keep the first one in two parts (1405/07/27).

### Welcome (`Welcome`, to the member)

Chosen by the developer on 1405/07/27 (2026-10-19), task 10.10. Sent when a member's first plan is
sold (BUSINESS_RULES.md §10 *The welcome*).

```
{name} عزیز، به باشگاه پاسارگاد خوش آمدید!
```

- Nothing in it would be wrong for a member moved over from the paper notebook, who gets it too.
- About 52 characters with a 10-character name: one part, like the absence SMS.

### Debt reminder (`DebtReminder`, to the member)

Chosen by the developer on 1405/07/27 (2026-10-19), task 10.11. Sent once per debt, when the
member's oldest unpaid sale is the settings' number of days old and their whole debt is at least the
minimum amount (BUSINESS_RULES.md §10 *The debt reminder*).

```
{name} عزیز، مانده حساب شما در باشگاه پاسارگاد {amount} تومان است. لطفاً در مراجعه بعدی تسویه بفرمایید. سپاس!
```

- `{amount}`: the member's whole open account in Toman, newer sales included, e.g. `۱٬۲۵۰٬۰۰۰`.
- Friendly rather than harsh, as asked; a shorter text (without «لطفاً … بفرمایید») was offered and
  would still take two parts, so the developer kept the polite one.
- About 120 characters with a 10-character name: two parts.

### Absence (`Absence`, to the member)

Chosen by the developer on 1405/07/27 (2026-10-19), task 10.12. Sent once per absence, to an active
member with a usable plan who has not come for the settings' number of days, counted from the last
visit or from the plan's start (BUSINESS_RULES.md §10 *The absence SMS*).

```
{name} عزیز، جای شما در باشگاه پاسارگاد خالی است!
```

- Friendly, with no offer in it (planned that way by the developer, 1405/07/15). A longer text
  («… منتظر دیدارتان هستیم.») was offered and would take two parts, so the developer chose the short one.
- About 59 characters with a 10-character name: one part.
