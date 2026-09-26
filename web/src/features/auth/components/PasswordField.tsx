import { Check, Eye, EyeOff, X } from "lucide-react";
import { useId, useState, type ChangeEvent, type ComponentProps } from "react";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

import { looksLikePersianKeyboard, passwordChecks } from "../password";

interface PasswordFieldProps extends Omit<ComponentProps<typeof Input>, "type"> {
  label: string;
  error?: string;
  /**
   * Starts with the text showing. For a temporary password the Owner types in order to read it
   * out to a staff member, hiding it would only get in the way.
   */
  defaultVisible?: boolean;
}

/**
 * Every password box in the app: login, change password, and the Owner's staff forms.
 *
 * - An eye button shows what was typed. NIST recommends it: a long password is easier to get
 *   right when it can be read back, and nobody at the front desk needs to be shoulder-surfed.
 * - A warning appears while the text has a Persian letter in it. The password policy is English
 *   only (docs/BUSINESS_RULES.md §1), and a keyboard left on Persian is the usual reason a
 *   correct password "does not work". Persian digits do not trigger it: they are converted.
 *
 * Uncontrolled, like `FormField`, so `form.register` wires it the same way. It keeps a copy of
 * the text only to decide whether to show the warning.
 */
export function PasswordField({
  label,
  error,
  defaultVisible = false,
  id,
  onChange,
  className,
  ...inputProps
}: PasswordFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;
  const warningId = `${inputId}-keyboard`;

  const [visible, setVisible] = useState(defaultVisible);
  const [persianKeyboard, setPersianKeyboard] = useState(false);

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    setPersianKeyboard(looksLikePersianKeyboard(event.target.value));
    onChange?.(event);
  }

  const describedBy = [error !== undefined ? errorId : "", persianKeyboard ? warningId : ""]
    .filter((part) => part !== "")
    .join(" ");

  return (
    <div className="space-y-2">
      <Label htmlFor={inputId}>{label}</Label>
      <span className="relative block">
        <Input
          id={inputId}
          type={visible ? "text" : "password"}
          dir="ltr"
          spellCheck={false}
          autoCapitalize="off"
          className={cn("pe-9", className)}
          aria-invalid={error !== undefined}
          aria-describedby={describedBy === "" ? undefined : describedBy}
          onChange={handleChange}
          {...inputProps}
        />
        <button
          type="button"
          aria-label={visible ? "پنهان کردن رمز عبور" : "نمایش رمز عبور"}
          aria-pressed={visible}
          className="absolute end-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          onClick={() => setVisible((current) => !current)}
        >
          {visible ? (
            <EyeOff className="size-4" aria-hidden />
          ) : (
            <Eye className="size-4" aria-hidden />
          )}
        </button>
      </span>
      {persianKeyboard && (
        <p id={warningId} role="status" className="text-sm text-warning">
          کیبورد روی فارسی است. رمز عبور با حروف انگلیسی نوشته می‌شود.
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

interface PasswordChecklistProps {
  /** What is in the new-password box now. */
  value: string;
  /** The account's user name, when known, for the "must not contain it" line. */
  userName?: string;
}

/**
 * The rules of a new password, each turning green as it is met. The blocklist of common
 * passwords is not here: only the server has it, and its answer appears as the field's error.
 */
export function PasswordChecklist({ value, userName }: PasswordChecklistProps) {
  const checks = passwordChecks(value, userName);

  return (
    <ul aria-label="شرایط رمز عبور" className="space-y-1 text-sm">
      {checks.map((check) => (
        <li
          key={check.key}
          data-met={check.met}
          className={cn(
            "flex items-center gap-2",
            check.met ? "text-success" : "text-muted-foreground",
          )}
        >
          {check.met ? (
            <Check className="size-4" aria-hidden />
          ) : (
            <X className="size-4" aria-hidden />
          )}
          <span>{check.label}</span>
          <span className="sr-only">{check.met ? "(رعایت شده)" : "(رعایت نشده)"}</span>
        </li>
      ))}
    </ul>
  );
}
