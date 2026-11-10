import { useState, type ReactNode } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";

import { BirthDateField, FormField, TextareaField } from "@/components/FormField";
import { SaveRow, type SaveNotice } from "@/components/SaveRow";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { SiteUserNotice } from "@/features/siteUsers/components/SiteUserNotice";
import { toneButton } from "@/lib/edges";
import { applyServerErrors, useUnchanged, zodResolver } from "@/lib/forms";
import { normalizeDigits, normalizePersianText } from "@/lib/normalize";

import type { Member, MemberInput } from "../api";
import { memberSchema, type MemberValues } from "../schemas";

/**
 * Errors the API reports for the whole request that are really about one field. They arrive as
 * a business failure (not a 400 with `errors`), because only the handler can tell: the phone
 * needs libphonenumber and the uniqueness needs the database.
 */
const codeFields = {
  "Members.PhoneAlreadyExists": "phoneNumber",
  "Members.PhoneBelongsToSiteUser": "phoneNumber",
  "Members.PhoneInvalid": "phoneNumber",
  "Members.PhoneNotMobile": "phoneNumber",
  "Members.PhoneNotIranian": "phoneNumber",
  "Members.PhoneRequired": "phoneNumber",
  "Members.FullNameRequired": "fullName",
  "Members.FullNameTooLong": "fullName",
  "Members.NotesTooLong": "notes",
  "Members.BirthDateRequired": "birthDate",
  "Members.BirthDateInFuture": "birthDate",
  "Members.BirthDateTooOld": "birthDate",
} as const;

interface MemberFormProps {
  defaultValues?: MemberValues;
  submitLabel: string;
  submittingLabel: string;
  /**
   * Sends the values. A thrown API problem is shown on the form; a caller that handles an
   * error itself (such as a concurrent edit) returns normally instead.
   */
  onSubmit: (input: MemberInput) => Promise<void>;
  /** Extra buttons beside submit, such as "cancel". */
  actions?: ReactNode;
  /**
   * Editing a member rather than adding one (roadmap 13.2c): «ذخیره» with nothing changed sends
   * nothing and says so, and every outcome is shown beside the button.
   */
  editing?: boolean;
  /** While editing: someone else saved first (a `StaleAlert`), shown beside the button. */
  stale?: ReactNode;
  /**
   * Adding a member: a whole phone that signed up on the site is offered «ثبت به‌عنوان عضو»
   * (BUSINESS_RULES.md §16 *Site users*), and this is told the member they became.
   */
  onSiteUserRegistered?: (member: Member) => void;
}

/** Values are normalized before they are sent, never while typing. */
function toInput(values: MemberValues): MemberInput {
  return {
    // ي/ك → ی/ک and spaces, so the stored name looks the same whichever keyboard typed it.
    fullName: normalizePersianText(values.fullName),
    // The API accepts Persian digits too; sending English ones keeps logs and audit readable.
    phoneNumber: normalizeDigits(values.phoneNumber).trim(),
    birthDate: values.birthDate,
    notes: values.notes === "" ? null : values.notes,
  };
}

/** The create and edit form. */
export function MemberForm({
  defaultValues = { fullName: "", phoneNumber: "", birthDate: "", notes: "" },
  submitLabel,
  submittingLabel,
  onSubmit,
  actions,
  editing = false,
  stale,
  onSiteUserRegistered,
}: MemberFormProps) {
  const form = useForm<MemberValues>({
    resolver: zodResolver(memberSchema),
    defaultValues,
  });
  const isUnchanged = useUnchanged(() => toInput(defaultValues));
  const [notice, setNotice] = useState<SaveNotice>(null);
  // For the site user's notice under the phone (BUSINESS_RULES.md §16).
  const phoneNumber = useWatch({ control: form.control, name: "phoneNumber" });

  const submit = form.handleSubmit(async (values) => {
    const input = toInput(values);
    if (editing && isUnchanged(input)) {
      setNotice("unchanged");
      return;
    }

    setNotice(null);
    try {
      await onSubmit(input);
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    // Every field is a native input, select or textarea, and their change events bubble up to here.
    <form
      className="grid max-w-xl gap-4"
      onSubmit={submit}
      onChange={() => setNotice(null)}
      noValidate
    >
      {!editing && errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <FormField
        label="نام و نام خانوادگی"
        autoComplete="off"
        error={errors.fullName?.message}
        {...form.register("fullName")}
      />
      <FormField
        label="شماره موبایل"
        dir="ltr"
        inputMode="tel"
        autoComplete="off"
        placeholder="۰۹۱۲ ۱۲۳ ۴۵۶۷"
        error={errors.phoneNumber?.message}
        {...form.register("phoneNumber")}
      />
      {!editing && onSiteUserRegistered !== undefined && (
        <SiteUserNotice phoneNumber={phoneNumber} onRegistered={onSiteUserRegistered} />
      )}
      <Controller
        control={form.control}
        name="birthDate"
        render={({ field }) => (
          // Not {...field}: this is not a native input, so field.ref has nowhere to go, and this
          // form does not use focus-on-error.
          <BirthDateField
            label="تاریخ تولد"
            error={errors.birthDate?.message}
            value={field.value}
            onChange={field.onChange}
            onBlur={field.onBlur}
          />
        )}
      />
      <TextareaField
        label="یادداشت (اختیاری)"
        rows={3}
        error={errors.notes?.message}
        {...form.register("notes")}
      />

      <SaveRow
        error={editing ? errors.root?.server?.message : undefined}
        notice={notice}
        stale={stale}
      >
        {/* Edged in the page's tone like the cafe's form buttons (`toneButton`, 13.2j). */}
        <Button type="submit" variant="ghost" className={toneButton} disabled={isSubmitting}>
          {isSubmitting ? submittingLabel : submitLabel}
        </Button>
        {actions}
      </SaveRow>
    </form>
  );
}
