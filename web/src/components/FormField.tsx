import { ChevronDown, X } from "lucide-react";
import { useEffect, useId, useRef, useState, type ComponentProps, type KeyboardEvent } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  amountInPersianWords,
  formatMoneyDigits,
  gymToday,
  jalaliMonthNames,
  jalaliPartsOf,
  jalaliToIso,
  toPersianDigits,
} from "@/lib/format";
import { moneyDigits } from "@/lib/money";
import { normalizeDigits } from "@/lib/normalize";
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
/** The look of a native `<select>` here, shared by `SelectField` and the date fields' boxes. */
const selectClassName = cn(
  "h-9 w-full min-w-0 rounded-md border border-input bg-transparent px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm",
  "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
  "aria-invalid:border-destructive aria-invalid:ring-destructive/20",
  // The box is transparent, so the browser draws the open list on its own white; the
  // options inherit the theme's text colour, which is light in the dark theme and was
  // unreadable. Giving the options the card colours keeps the list readable in both.
  "[&_option]:bg-card [&_option]:text-card-foreground",
);

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
        className={cn(selectClassName, className)}
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
 * Controlled, because a field that shows what the amount *means* has to know what the amount
 * is. The value it holds is the text on screen, and `normalizeMoney` turns that into the plain
 * decimal string the API reads when the form is sent — no money value is ever a JavaScript number
 * on the way.
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

/** The birth years offered, newest first (asked by the developer, 1405/07/12). */
const firstBirthYear = 1320;
const lastBirthYear = 1400;

/**
 * The years every other date is chosen from (chosen by the developer, 1405/07/13): from ۱۴۰۴ up to
 * three years past this one, so a cheque due years ahead can still be entered and the list grows
 * by itself each Nowruz.
 */
const firstBusinessYear = 1404;
const businessYearsAhead = 3;

/** How many days a Jalali month has. Esfand has 30 only in a leap year; with no year yet, 30. */
function daysInJalaliMonth(year: number | null, month: number): number {
  if (month <= 6) {
    return 31;
  }
  if (month <= 11) {
    return 30;
  }
  return year === null || jalaliToIso(year, 12, 30) !== null ? 30 : 29;
}

interface DateParts {
  year: string;
  month: string;
  day: string;
}

const noDateParts: DateParts = { year: "", month: "", day: "" };

function datePartsOf(value: string): DateParts {
  const parts = jalaliPartsOf(value);

  return parts === null
    ? noDateParts
    : { year: String(parts.year), month: String(parts.month), day: String(parts.day) };
}

interface PickerOption {
  value: string;
  label: string;
}

interface PickerProps {
  /** The box's own name, read with the group's: «روز», «ماه», «سال». Shown until a choice is made. */
  name: string;
  options: PickerOption[];
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  invalid: boolean;
  disabled?: boolean;
}

/** How long typed characters keep adding to one search («۱», «۱۳», «۱۳۷», «۱۳۷۰»). */
const typeAheadMs = 1000;

/**
 * One of a date field's three boxes. Not a native `<select>`: the browser decides how tall a
 * native list opens, and on a desktop the day and year lists filled the screen. This one opens a
 * short list (about six rows) that scrolls, the same height for all three boxes.
 *
 * It follows the WAI-ARIA "select-only combobox" pattern: focus stays on the button, the list is
 * a `listbox`, and `aria-activedescendant` says which row the arrow keys are on. Arrow keys,
 * Home/End, Enter/Space and Escape work as in a native select, and typing jumps to the first row
 * that starts with what was typed, in Persian or English digits («1370» finds ۱۳۷۰). Escape
 * closes the list only, not the dialog around it.
 *
 * The list opens under the box, inside the page's flow of positioning (not a portal), so in a
 * dialog it scrolls with the dialog; opening it scrolls the dialog just enough to show it.
 */
function Picker({ name, options, value, onChange, onBlur, invalid, disabled }: PickerProps) {
  const listId = useId();
  const optionId = (index: number) => `${listId}-${index}`;
  const selectedIndex = options.findIndex((option) => option.value === value);

  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const typed = useRef({ text: "", at: 0 });
  const list = useRef<HTMLUListElement>(null);

  // Keeps the row the keys are on in view, and on opening scrolls a dialog so the list shows.
  useEffect(() => {
    if (!open) {
      return;
    }
    list.current?.scrollIntoView?.({ block: "nearest" });
    document.getElementById(`${listId}-${active}`)?.scrollIntoView?.({ block: "nearest" });
  }, [open, active, listId]);

  function show() {
    setActive(selectedIndex === -1 ? 0 : selectedIndex);
    setOpen(true);
  }

  function pick(index: number) {
    const option = options[index];
    if (option !== undefined) {
      onChange(option.value);
    }
    setOpen(false);
  }

  function typeAhead(key: string) {
    const now = Date.now();
    const text = (now - typed.current.at < typeAheadMs ? typed.current.text : "") + key;
    typed.current = { text, at: now };

    const wanted = normalizeDigits(text);
    const found = options.findIndex((option) => normalizeDigits(option.label).startsWith(wanted));
    const option = options[found];
    if (option === undefined) {
      return;
    }
    if (open) {
      setActive(found);
    } else {
      onChange(option.value);
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const last = options.length - 1;
    const moves: Record<string, number> = {
      ArrowDown: Math.min(active + 1, last),
      ArrowUp: Math.max(active - 1, 0),
      Home: 0,
      End: last,
      PageDown: Math.min(active + 5, last),
      PageUp: Math.max(active - 5, 0),
    };

    if (event.key in moves) {
      event.preventDefault();
      if (open) {
        setActive(moves[event.key] ?? active);
      } else {
        show();
      }
    } else if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      if (open) {
        pick(active);
      } else {
        show();
      }
    } else if (event.key === "Escape" && open) {
      // Closes the list only, not the dialog around it.
      event.stopPropagation();
      setOpen(false);
    } else if (event.key === "Tab") {
      setOpen(false);
    } else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      typeAhead(event.key);
    }
  }

  const chosen = options[selectedIndex];

  return (
    <div className="relative">
      <button
        type="button"
        role="combobox"
        aria-label={name}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        aria-activedescendant={open ? optionId(active) : undefined}
        aria-invalid={invalid}
        disabled={disabled}
        className={cn(
          selectClassName,
          "flex items-center justify-between gap-1 text-start",
          chosen === undefined && "text-muted-foreground",
        )}
        onClick={() => (open ? setOpen(false) : show())}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          setOpen(false);
          onBlur?.();
        }}
      >
        <span className="truncate">{chosen?.label ?? name}</span>
        <ChevronDown className="size-4 shrink-0 opacity-50" aria-hidden />
      </button>
      {open && (
        <ul
          ref={list}
          id={listId}
          role="listbox"
          aria-label={name}
          // Keeps focus on the button, so its blur means "done" and a click here is not one.
          onMouseDown={(event) => event.preventDefault()}
          className="absolute inset-x-0 top-full z-50 mt-1 max-h-52 overflow-y-auto rounded-md border bg-card p-1 text-card-foreground shadow-md"
        >
          {options.map((option, index) => (
            <li
              key={option.value}
              id={optionId(index)}
              role="option"
              aria-selected={index === selectedIndex}
              className={cn(
                "cursor-pointer rounded-sm px-2 py-1.5 text-sm",
                index === active && "bg-accent text-accent-foreground",
                index === selectedIndex && "font-semibold text-primary",
              )}
              onMouseEnter={() => setActive(index)}
              onClick={() => pick(index)}
            >
              {option.label}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

interface DateFieldProps {
  label: string;
  error?: string;
  id?: string;
  /** The ISO business date the API stores (`1991-08-03`), or `""` when there is none. */
  value: string;
  onChange: (iso: string) => void;
  onBlur?: () => void;
  disabled?: boolean;
}

interface DropdownDateFieldProps extends DateFieldProps {
  /** The oldest and newest years the year list offers. */
  firstYear: number;
  lastYear: number;
  /** Whether a «پاک کردن» button can take the date away again (a filter left open). */
  clearable: boolean;
}

/**
 * A date as three dropdowns — day, month by name, year — the way mobile banking apps ask for it
 * (asked by the developer, 1405/07/12 for a birth date, 1405/07/13 for every other date). Three
 * short lists reach any date in three choices, and every date in the app is chosen the same way.
 * The three boxes are the same width and open lists of the same height (see `Picker`).
 *
 * The value is the ISO Gregorian date the API stores, converted by `jalaliToIso`. Until all three
 * are chosen the value is `""`, so a half-chosen date is refused as "no date" rather than saved as
 * something else. The day list follows the month (and Esfand follows the year); a day the new
 * month lacks becomes its last day.
 *
 * In reading order (right to left) it is day, month, year — «۱۲ مرداد ۱۳۷۰», the way a date is
 * said. A stored date outside the year range still shows, with its year added to the list.
 */
function DropdownDateField({
  label,
  error,
  id,
  value,
  onChange,
  onBlur,
  disabled,
  firstYear,
  lastYear,
  clearable,
}: DropdownDateFieldProps) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  const labelId = `${fieldId}-label`;
  const errorId = `${fieldId}-error`;

  // The three choices. They follow `value` when the form supplies a new one (an edit form
  // finishing its load, a reset) but not while this field is what changes it, so a half-chosen
  // date — which sends "" — does not wipe the choices already made.
  const [parts, setParts] = useState(() => datePartsOf(value));
  const [committed, setCommitted] = useState(value);
  if (value !== committed) {
    setCommitted(value);
    setParts(datePartsOf(value));
  }

  const chosenYear = parts.year === "" ? null : Number(parts.year);
  const newest = Math.max(lastYear, chosenYear ?? lastYear);
  const oldest = Math.min(firstYear, chosenYear ?? firstYear);
  const years = Array.from({ length: newest - oldest + 1 }, (_, index) => newest - index);
  const dayCount = daysInJalaliMonth(chosenYear, parts.month === "" ? 1 : Number(parts.month));

  function commit(next: DateParts) {
    const iso =
      next.year === "" || next.month === "" || next.day === ""
        ? ""
        : (jalaliToIso(Number(next.year), Number(next.month), Number(next.day)) ?? "");

    setParts(next);
    setCommitted(iso);
    onChange(iso);
  }

  function choose(change: Partial<DateParts>) {
    const next = { ...parts, ...change };
    if (next.month !== "" && next.day !== "") {
      const last = daysInJalaliMonth(
        next.year === "" ? null : Number(next.year),
        Number(next.month),
      );
      if (Number(next.day) > last) {
        next.day = String(last);
      }
    }

    commit(next);
  }

  const anythingChosen = parts.year !== "" || parts.month !== "" || parts.day !== "";
  const shared = { onBlur, disabled, invalid: error !== undefined };

  return (
    <div className="space-y-2">
      {/* The clear button sits beside the label, not beside the boxes: in a four-column filter
          row the three boxes need all the width there is to show a month's name. */}
      <div className="flex items-center justify-between gap-2">
        <Label id={labelId}>{label}</Label>
        {clearable && anythingChosen && disabled !== true && (
          <button
            type="button"
            aria-label={`پاک کردن ${label}`}
            className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
            onClick={() => commit(noDateParts)}
          >
            <X className="size-3.5" aria-hidden />
            پاک کردن
          </button>
        )}
      </div>
      <div
        role="group"
        aria-labelledby={labelId}
        aria-describedby={error !== undefined ? errorId : undefined}
        className="grid grid-cols-3 gap-2"
      >
        <Picker
          {...shared}
          name="روز"
          value={parts.day}
          onChange={(day) => choose({ day })}
          options={Array.from({ length: dayCount }, (_, index) => ({
            value: String(index + 1),
            label: toPersianDigits(index + 1),
          }))}
        />
        <Picker
          {...shared}
          name="ماه"
          value={parts.month}
          onChange={(month) => choose({ month })}
          options={jalaliMonthNames.map((monthName, index) => ({
            value: String(index + 1),
            label: monthName,
          }))}
        />
        <Picker
          {...shared}
          name="سال"
          value={parts.year}
          onChange={(year) => choose({ year })}
          options={years.map((year) => ({ value: String(year), label: toPersianDigits(year) }))}
        />
      </div>
      {error !== undefined && (
        <p id={errorId} className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

/**
 * `FormField` for a business date (docs/BUSINESS_RULES.md §13): a cheque's date, an expense's
 * date, either end of a filter. Chosen the same way as a birth date, with the years running from
 * three years ahead back to ۱۴۰۴, and a «پاک کردن» button, because an empty date is how a filter
 * says "no limit".
 */
export function JalaliDateField(props: DateFieldProps) {
  const thisYear = jalaliPartsOf(gymToday())?.year ?? firstBusinessYear;

  return (
    <DropdownDateField
      {...props}
      firstYear={firstBusinessYear}
      lastYear={thisYear + businessYearsAhead}
      clearable
    />
  );
}

/** A birth date: the years run from ۱۴۰۰ back to ۱۳۲۰ (the API's rules of §2 still apply). */
export function BirthDateField(props: DateFieldProps) {
  return (
    <DropdownDateField
      {...props}
      firstYear={firstBirthYear}
      lastYear={lastBirthYear}
      clearable={false}
    />
  );
}
