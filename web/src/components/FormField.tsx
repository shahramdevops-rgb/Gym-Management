import { X } from "lucide-react";
import { useId, useState, type ChangeEvent, type ComponentProps } from "react";
import persian from "react-date-object/calendars/persian";
import persian_fa from "react-date-object/locales/persian_fa";
import { Calendar, DateObject } from "react-multi-date-picker";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  amountInPersianWords,
  formatMoneyDigits,
  jalaliPartsOf,
  jalaliToIso,
  toIsoDate,
  toJalaliInput,
} from "@/lib/format";
import { moneyDigits } from "@/lib/money";
import { cn } from "@/lib/utils";

interface FormFieldProps extends ComponentProps<typeof Input> {
  label: string;
  error?: string;
}

/**
 * A labelled input with its error underneath, wired for screen readers: the input is marked
 * invalid and points at the message, so the message is read when the field gets focus.
 */
export function FormField({ label, error, id, ...inputProps }: FormFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>{label}</Label>
      <Input
        id={inputId}
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? errorId : undefined}
        {...inputProps}
      />
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

interface SelectFieldProps extends ComponentProps<"select"> {
  label: string;
  error?: string;
}

/**
 * FormField for a fixed set of choices. A plain native `<select>`, styled to match `Input`,
 * rather than a Radix component: this codebase already prefers a native control over a new
 * dependency for simple cases (the checkbox in `PlanForm`).
 */
export function SelectField({
  label,
  error,
  id,
  className,
  children,
  ...selectProps
}: SelectFieldProps) {
  const generatedId = useId();
  const selectId = id ?? generatedId;
  const errorId = `${selectId}-error`;

  return (
    <div className="space-y-2">
      <Label htmlFor={selectId}>{label}</Label>
      <select
        id={selectId}
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? errorId : undefined}
        className={cn(
          "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm",
          "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
          "aria-invalid:border-destructive aria-invalid:ring-destructive/20",
          // The box is transparent, so the browser draws the open list on its own white; the
          // options inherit the theme's text colour, which is light in the dark theme and was
          // unreadable. Giving the options the card colours keeps the list readable in both.
          "[&_option]:bg-card [&_option]:text-card-foreground",
          className,
        )}
        {...selectProps}
      >
        {children}
      </select>
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

interface MoneyFieldProps {
  label: string;
  error?: string;
  id?: string;
  name?: string;
  /** The text in the box, grouped and in Persian digits — what `normalizeMoney` turns back into a number. */
  value: string;
  onChange: (text: string) => void;
  onBlur?: () => void;
  disabled?: boolean;
  placeholder?: string;
  /** The box may be left blank. The line underneath then says so, rather than going quiet. */
  optional?: boolean;
}

/**
 * `FormField` for an amount of money. Every amount that is typed anywhere in the app goes
 * through this one field (docs/BUSINESS_RULES.md §13), because a Toman figure is long enough
 * that ۵۰۰٬۰۰۰ and ۵٬۰۰۰٬۰۰۰ look alike, and a wrong figure here is a wrong figure in the gym's
 * books. Two things guard against that:
 *
 * - digits are grouped in threes as they are typed (۹۰۰٬۰۰۰), the way mobile banking apps do;
 * - the same amount is written out in words underneath — «نهصد هزار تومان». The words are the
 *   real check: a run of zeros can be miscounted, a word cannot. They are wired into
 *   `aria-describedby` too, so the check is read aloud and not merely seen.
 *
 * Controlled, like `JalaliDateField` below and for the same reason: a field that shows what the
 * amount *means* has to know what the amount is. The value it holds is the text on screen, and
 * `normalizeMoney` turns that into the plain decimal string the API reads when the form is
 * sent — no money value is ever a JavaScript number on the way.
 *
 * The caret is not preserved through a reformat (it lands at the end), which fits how an amount
 * is actually typed here: once, left to right, not edited digit-by-digit in the middle.
 */
export function MoneyField({
  label,
  error,
  id,
  name,
  value,
  onChange,
  onBlur,
  disabled,
  placeholder,
  optional = false,
}: MoneyFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;
  const wordsId = `${inputId}-words`;

  // Formatted on the way out as well as on the way in, so a value the form supplied itself —
  // an edit form loading a plan's price as a bare `1500000` — is grouped from the first paint.
  const text = formatMoneyDigits(moneyDigits(value));
  const words = text === "" ? (optional ? "بدون مبلغ" : "") : amountInPersianWords(text);

  const describedBy = [error !== undefined ? errorId : "", words !== "" ? wordsId : ""]
    .filter((part) => part !== "")
    .join(" ");

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>{label}</Label>
      <Input
        id={inputId}
        name={name}
        dir="ltr"
        inputMode="decimal"
        autoComplete="off"
        value={text}
        disabled={disabled}
        placeholder={placeholder}
        aria-invalid={error !== undefined}
        aria-describedby={describedBy === "" ? undefined : describedBy}
        onChange={(event) => onChange(formatMoneyDigits(moneyDigits(event.target.value)))}
        onBlur={onBlur}
      />
      {words !== "" && (
        <p id={wordsId} className="text-sm text-muted-foreground">
          {words}
        </p>
      )}
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

interface TextareaFieldProps extends ComponentProps<typeof Textarea> {
  label: string;
  error?: string;
}

/** FormField for multi-line text, with the same label and error wiring. */
export function TextareaField({ label, error, id, ...textareaProps }: TextareaFieldProps) {
  const generatedId = useId();
  const textareaId = id ?? generatedId;
  const errorId = `${textareaId}-error`;

  return (
    <div className="space-y-2">
      <Label htmlFor={textareaId}>{label}</Label>
      <Textarea
        id={textareaId}
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? errorId : undefined}
        {...textareaProps}
      />
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

interface JalaliDateFieldProps {
  label: string;
  error?: string;
  id?: string;
  name?: string;
  /** The ISO business date the API stores (`1991-08-03`), or `""` when there is none. */
  value: string;
  onChange: (iso: string) => void;
  onBlur?: () => void;
  disabled?: boolean;
  placeholder?: string;
}

/**
 * `FormField` for a business date (docs/BUSINESS_RULES.md §13): the box speaks Jalali, the value
 * it holds is the ISO Gregorian date the API stores, and the two never mix.
 *
 * The calendar comes from react-multi-date-picker; the input does not. It is this app's `Input`,
 * for three reasons: it carries `aria-invalid` and `aria-describedby` like every other field here;
 * typing is parsed by `toIsoDate`, the one conversion this app owns and tests, rather than by a
 * second parser that does not know Arabic-Indic digits; and the clear button has somewhere to live.
 *
 * **The calendar opens in place, under the box, not as a floating popup.** Nearly every date
 * field lives in a dialog, and a dialog scrolls: a popup was clipped at the dialog's edge, landed
 * over the text and buttons beside it, and in right-to-left lost track of which side "start" was.
 * In the page's own flow it pushes what follows down, takes the field's width on any screen, and
 * the dialog simply scrolls to it. It opens when the box is focused or clicked, and closes when a
 * day is picked, on Escape, or when focus leaves the box. Pressing inside the calendar does not
 * take focus from the box (its mousedown is cancelled), so turning the month does not close it.
 *
 * What is typed is committed on every keystroke that forms a whole, real date, and the box snaps
 * back to the committed date on blur — so what is on screen is always what will be sent, and a
 * half-typed date is visibly discarded rather than quietly saved as "no birth date".
 *
 * No `dir="ltr"`, unlike `MoneyField`: the slashes in ۱۳۷۰/۰۵/۱۲ are bidi common separators and
 * hold the digit runs together on their own, which the phone number's spaces do not.
 */
export function JalaliDateField({
  label,
  error,
  id,
  name,
  value,
  onChange,
  onBlur,
  disabled,
  placeholder = "۱۳۷۰/۰۵/۱۲",
}: JalaliDateFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;
  const calendarId = `${inputId}-calendar`;

  // The text in the box. It follows `value` when the form supplies a new one (an edit form
  // finishing its load, a reset) but not while this field is what changes it, so a half-typed
  // date is never reformatted under the caret.
  const [text, setText] = useState(() => toJalaliInput(value));
  const [committed, setCommitted] = useState(value);
  const [open, setOpen] = useState(false);
  if (value !== committed) {
    setCommitted(value);
    setText(toJalaliInput(value));
  }

  function commit(iso: string) {
    setCommitted(iso);
    onChange(iso);
  }

  function handleTyping(event: ChangeEvent<HTMLInputElement>) {
    setText(event.target.value);
    commit(toIsoDate(event.target.value) ?? "");
  }

  function handlePicked(picked: DateObject | null) {
    // `calendar={persian}` means these are Jalali numbers. format.ts owns the conversion, so the
    // calendar and the typed box can never disagree about which day was chosen.
    const iso =
      picked === null ? "" : (jalaliToIso(picked.year, picked.month.number, picked.day) ?? "");

    setText(toJalaliInput(iso));
    commit(iso);
    setOpen(false);
  }

  const parts = jalaliPartsOf(value);
  const showCalendar = open && disabled !== true;

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>{label}</Label>
      <span className="relative block">
        <Input
          id={inputId}
          name={name}
          value={text}
          disabled={disabled}
          placeholder={placeholder}
          autoComplete="off"
          className="pe-9"
          aria-invalid={error !== undefined}
          aria-describedby={error !== undefined ? errorId : undefined}
          aria-expanded={showCalendar}
          aria-controls={showCalendar ? calendarId : undefined}
          onChange={handleTyping}
          onFocus={() => setOpen(true)}
          onClick={() => setOpen(true)}
          onKeyDown={(event) => {
            if (event.key === "Escape" && open) {
              // Closes the calendar only, not the dialog around it.
              event.stopPropagation();
              setOpen(false);
            }
          }}
          onBlur={() => {
            setOpen(false);
            setText(toJalaliInput(committed));
            onBlur?.();
          }}
        />
        {text !== "" && disabled !== true && (
          <button
            type="button"
            aria-label="پاک کردن تاریخ"
            className="absolute end-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
            onClick={() => {
              setText("");
              commit("");
            }}
          >
            <X className="size-4" aria-hidden />
          </button>
        )}
      </span>
      {showCalendar && (
        // Keeps focus in the box while the calendar is used, so the box's blur means "done".
        <div id={calendarId} onMouseDown={(event) => event.preventDefault()}>
          <Calendar
            calendar={persian}
            locale={persian_fa}
            shadow={false}
            className="jalali-calendar rmdp-border"
            value={
              parts === null
                ? null
                : new DateObject({ ...parts, calendar: persian, locale: persian_fa })
            }
            onChange={handlePicked}
          />
        </div>
      )}
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
