import type { ReactNode } from "react";
import { useForm } from "react-hook-form";

import { FormField, TextareaField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeDigits, normalizePersianText } from "@/lib/normalize";

import type { MemberInput } from "../api";
import { memberSchema, type MemberValues } from "../schemas";

/**
 * Errors the API reports for the whole request that are really about one field. They arrive as
 * a business failure (not a 400 with `errors`), because only the handler can tell: the phone
 * needs libphonenumber and the uniqueness needs the database.
 */
const codeFields = {
  "Members.PhoneAlreadyExists": "phoneNumber",
  "Members.PhoneInvalid": "phoneNumber",
  "Members.PhoneNotMobile": "phoneNumber",
  "Members.PhoneNotIranian": "phoneNumber",
  "Members.PhoneRequired": "phoneNumber",
  "Members.FullNameRequired": "fullName",
  "Members.FullNameTooLong": "fullName",
  "Members.NotesTooLong": "notes",
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
}

/** The create and edit form. Values are normalized before they are sent, never while typing. */
export function MemberForm({
  defaultValues = { fullName: "", phoneNumber: "", notes: "" },
  submitLabel,
  submittingLabel,
  onSubmit,
  actions,
}: MemberFormProps) {
  const form = useForm<MemberValues>({
    resolver: zodResolver(memberSchema),
    defaultValues,
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit({
        // ي/ك → ی/ک and spaces, so the stored name looks the same whichever keyboard typed it.
        fullName: normalizePersianText(values.fullName),
        // The API accepts Persian digits too; sending English ones keeps logs and audit readable.
        phoneNumber: normalizeDigits(values.phoneNumber).trim(),
        notes: values.notes === "" ? null : values.notes,
      });
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="grid max-w-xl gap-4" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
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
      <TextareaField
        label="یادداشت (اختیاری)"
        rows={3}
        error={errors.notes?.message}
        {...form.register("notes")}
      />

      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? submittingLabel : submitLabel}
        </Button>
        {actions}
      </div>
    </form>
  );
}
