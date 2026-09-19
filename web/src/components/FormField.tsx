import { useId, type ChangeEvent, type ComponentProps } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toPersianDigits } from "@/lib/format";
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
export function SelectField({ label, error, id, className, children, ...selectProps }: SelectFieldProps) {
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
