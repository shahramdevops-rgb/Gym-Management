# SMS texts

The wording of every SMS the gym sends (BUSINESS_RULES.md §10). Agreed with the developer on
1405/07/14 (2026-10-06), task 10.1.

The wording lives **in the code** (`SmsText` in `Gym.Domain/Notifications`), and the system sends
the whole text with Kavenegar's `sms/send` from the gym's dedicated line. These were first written
as Kavenegar templates, but Kavenegar refused every template (the gym's site was not up yet), so the
gym bought a dedicated line, which needs none (1405/07/16). Changing a text is a release: change it
here and in `SmsText` together, and the domain tests that hold each text.

## The values

Numbers and dates are written the way the app shows them: Persian digits, the Jalali date as
`۱۴۰۵/۰۷/۲۰`, an amount with thousands separators (`٬`) and no space.

A long name or payee is **simply cut short** (decided with the developer when these were templates,
and kept so one long name cannot make a message cost several parts): a member's name keeps its first
6 words and a payee its first 9, then the first 100 characters (`SmsText.Fit`).

**Length:** a Persian SMS holds 70 characters in one part and 67 in each part of a longer one; each
part is paid for. The lengths below take a 10-character name and a 20-character payee. The developer
decided that length is not a concern for these four.

## The texts

`{name}` and the other braces mark where a value goes.

### Subscription running out (`SubscriptionExpiring`, to the member)

```
{name} عزیز، اشتراک شما در باشگاه پاسارگاد {end date} به پایان می‌رسد.
```

- The end date and not "n days left", which would read «۰ روز دیگر» on the last day.
- About 74 characters: two parts.

### Few sessions left (`LowSessions`, to the member)

```
{name} عزیز، فقط {sessions left} جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.
```

- About 71 characters: two parts, one with a name a character or two shorter.

### Birthday (`Birthday`, to the member)

Two texts, because the Owner may send it 0 to 7 days ahead, and «تولدتان مبارک» three days early
reads wrong. The system picks by the birthday's date: the first on the day itself, the second when
it is 1 to 7 days away.

```
{name} عزیز، امروز {birthday} روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد
```

```
{name} عزیز، تولدتان در {birthday} را پیشاپیش تبریک می‌گوییم. باشگاه پاسارگاد
```

- `{birthday}`: the birthday's date this year (Jalali); 29 Esfand for someone born on 30 Esfand in a
  year without it.
- About 80 and 81 characters: two parts each.

### Cheque or instalment coming due (`PayableDue`, to the Owner)

```
یادآوری {چک or قسط}: {amount} تومان، سررسید {date}، به {payee}
```

- `{date}`: the date written on the cheque or the instalment's due day (Jalali).
- `{amount}`: in Toman, e.g. `۱۲٬۵۰۰٬۰۰۰`.
- About 72 characters: two parts, one with a shorter payee. One SMS for each cheque and each instalment.
