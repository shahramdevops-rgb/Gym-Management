# SMS templates

The wording of every SMS the gym sends (BUSINESS_RULES.md §10). Agreed with the developer on
1405/07/14 (2026-10-06), task 10.1.

The wording lives in **Kavenegar**, not in the code: each template below is made in the Kavenegar
panel (*ارسال پیامک › اعتبارسنجی › قالب جدید*), with the **same name**, and approved there before it
can be used. The system sends only the template's name and the values for its blanks. This file
exists so the same templates can be made again in another account: the developer's for testing,
the Owner's at release (roadmap 10.6).

## The blanks

| Blank | Holds | Used for |
|---|---|---|
| `%token` | no space, at most 100 characters. **Every template needs it**: Kavenegar refuses a message without it | a date, a number, «چک» or «قسط» |
| `%token2`, `%token3` | no space, at most 100 characters | an amount, a date |
| `%token10` | up to 5 spaces, at most 100 characters | the member's name |
| `%token20` | up to 8 spaces, at most 100 characters | the payee |

A value longer than its blank allows is **simply cut short**: only the words the blank allows are
kept, then the first 100 characters (`SmsTokens.Fit`, decided with the developer). Numbers and dates
are written the way the app shows them: Persian digits, the Jalali date as `۱۴۰۵/۰۷/۲۰`, an amount
with thousands separators and no space.

**Length:** a Persian SMS holds 70 characters in one part and 67 in each part of a longer one; each
part is paid for. The lengths below take a 10-character name and a 20-character payee. The developer
decided that length is not a concern for these four.

## The templates

The template names follow Kavenegar's rule: English letters and digits, no space, no `_`. The
Owner types the name of each kind's template on the SMS settings page (task 10.2).

### `gymExpiring`: subscription running out (`SubscriptionExpiring`, to the member)

```
%token10 عزیز، اشتراک شما در باشگاه پاسارگاد %token به پایان می‌رسد.
```

- `%token`: the subscription's end date (Jalali), e.g. `۱۴۰۵/۰۷/۲۰`. The date and not "n days
  left", which would read «۰ روز دیگر» on the last day.
- `%token10`: the member's name.
- About 74 characters: two parts.

### `gymLowSessions`: few sessions left (`LowSessions`, to the member)

```
%token10 عزیز، فقط %token جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.
```

- `%token`: the sessions left, e.g. `۲`.
- `%token10`: the member's name.
- About 71 characters: two parts, one with a name a character or two shorter.

### `gymBirthday` and `gymBirthdayEarly`: birthday (`Birthday`, to the member)

Two templates, because the Owner may send it 0 to 7 days ahead, and «تولدتان مبارک» three days early
reads wrong. The Owner picks the one that matches the days set on the settings page: `gymBirthday`
for 0 (the day itself), `gymBirthdayEarly` for 1 to 7.

```
%token10 عزیز، امروز %token روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد
```

```
%token10 عزیز، تولدتان در %token را پیشاپیش تبریک می‌گوییم. باشگاه پاسارگاد
```

- `%token`: the birthday's date this year (Jalali), e.g. `۱۴۰۵/۰۷/۲۰`; 29 Esfand for someone born on
  30 Esfand in a year without it. The date is there because Kavenegar needs `%token` in every
  template.
- `%token10`: the member's name.
- About 80 and 81 characters: two parts each.
- **Still to confirm with Kavenegar's support** that a birthday greeting is accepted as a template
  (BUSINESS_RULES.md §0). If it is not, the birthday alone is sent as free text from a line.

### `gymPayableDue`: cheque or instalment coming due (`PayableDue`, to the Owner)

```
یادآوری %token: %token2 تومان، سررسید %token3، به %token20
```

- `%token`: «چک» or «قسط».
- `%token2`: the amount in Toman, e.g. `۱۲٬۵۰۰٬۰۰۰`.
- `%token3`: the date written on the cheque or the instalment's due day (Jalali).
- `%token20`: the payee, cut short when longer than the blank allows.
- About 72 characters: two parts, one with a shorter payee. One SMS for each cheque and each instalment.
