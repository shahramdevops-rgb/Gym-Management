import { X } from "lucide-react";
import { useId, useState, type ChangeEvent, type ComponentProps } from "react";
import persian from "react-date-object/calendars/persian";
import persian_fa from "react-date-object/locales/persian_fa";
import DatePickerModule, { DateObject } from "react-multi-date-picker";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  jalaliPartsOf,
  jalaliToIso,
  toIsoDate,
  toJalaliInput,
  toPersianDigits,
} from "@/lib/format";
import { normalizeDigits } from "@/lib/normalize";
import { cn } from "@/lib/utils";

/**
 * react-multi-date-picker ships CommonJS only and puts its component on `exports.default`.
 * Vite pre-bundles the package and hands a default import the whole module object, so rendering
 * it directly fails in the browser with "Element type is invalid ... got: object" — while
 * Vitest's own interop hands back the component, so every test passes. Unwrap whichever arrived.
 *
 * `DateObject` and the calendar and locale modules need none of this: they are read as named
 * properties, or exported as a plain `module.exports` object with no `default` to unwrap.
 */
const DatePicker =
  (DatePickerModule as unknown as { default?: typeof DatePickerModule }).default ??
  DatePickerModule;

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

/** A typed amount with the separators stripped back out, the same shape `amountProblem` expects. */
function cleanAmountDigits(raw: string): string {
  const ascii = normalizeDigits(raw).replace(/[,٬]/g, "").replace(/٫/g, ".");
  const onlyDigitsAndDot = ascii.replace(/[^\d.]/g, "");
  const firstDot = onlyDigitsAndDot.indexOf(".");
  if (firstDot === -1) {
    return onlyDigitsAndDot;
  }
  return `${onlyDigitsAndDot.slice(0, firstDot)}.${onlyDigitsAndDot.slice(firstDot + 1).replace(/\./g, "")}`;
}

/** The same digits, grouped by thousands with Persian digits, for display only. */
function formatAmountDigits(digits: string): string {
  if (digits === "") {
    return "";
  }
  const [whole = "", fraction] = digits.split(".");
  const groupedWhole = whole.replace(/\B(?=(\d{3})+(?!\d))/g, "٬");
  return toPersianDigits(fraction === undefined ? groupedWhole : `${groupedWhole}٫${fraction}`);
}

interface MoneyFieldProps extends ComponentProps<typeof Input> {
  label: string;
  error?: string;
}

/**
 * `FormField` for a money amount, grouping digits by thousands as you type (۹۰۰٬۰۰۰) the way
 * mobile banking apps do — a long run of zeros is easy to miscount otherwise. It stays an
 * uncontrolled input wired through `register()`, same as every other field here: the reformat
 * happens on the native `input` value before `onChange` (from `register()`) ever sees it, so
 * react-hook-form stores exactly what's on screen. That is still valid input downstream —
 * `amountProblem`/`normalizeAmount` already strip separators and convert Persian digits, since
 * typing a formatted amount by hand was always allowed. The caret is not preserved through a
 * reformat (it lands at the end), which fits how an amount is actually typed here: once, left to
 * right, not edited digit-by-digit in the middle.
 */
export function MoneyField({ label, error, id, onChange, ...inputProps }: MoneyFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    event.target.value = formatAmountDigits(cleanAmountDigits(event.target.value));
    onChange?.(event);
  }

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>{label}</Label>
      <Input
        id={inputId}
        dir="ltr"
        inputMode="decimal"
        autoComplete="off"
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? errorId : undefined}
        {...inputProps}
        onChange={handleChange}
      />
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
 * The calendar comes from react-multi-date-picker; the input does not. `render` replaces the
 * library's own input with this app's `Input`, for three reasons: the library's input accepts no
 * `aria-invalid` or `aria-describedby`, which every other field here has and the tests assert;
 * typing is then parsed by `toIsoDate`, the one conversion this app owns and tests, rather than
 * by a second parser that does not know Arabic-Indic digits; and the clear button has somewhere
 * to live.
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

  // The text in the box. It follows `value` when the form supplies a new one (an edit form
  // finishing its load, a reset) but not while this field is what changes it, so a half-typed
  // date is never reformatted under the caret.
  const [text, setText] = useState(() => toJalaliInput(value));
  const [committed, setCommitted] = useState(value);
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
  }

  const parts = jalaliPartsOf(value);

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>{label}</Label>
      <DatePicker
        calendar={persian}
        locale={persian_fa}
        format="YYYY/MM/DD"
        calendarPosition="bottom-start"
        containerClassName="block w-full"
        value={
          parts === null ? null : new DateObject({ ...parts, calendar: persian, locale: persian_fa })
        }
        onChange={handlePicked}
        render={(_value, openCalendar) => (
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
              onChange={handleTyping}
              onFocus={openCalendar}
              onBlur={() => {
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
        )}
      />
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
