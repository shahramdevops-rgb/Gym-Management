import { useForm } from "react-hook-form";
import { z } from "zod";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { errorMessages } from "@/lib/errors";
import { applyServerErrors, zodResolver } from "@/lib/forms";

/** The same limit as a member's name (BUSINESS_RULES.md §7 *Guest visit*, `Attendance.GuestNameMaxLength`). */
export const guestNameMaxLength = 200;

const guestNameSchema = z.object({
  guestName: z
    .string()
    .trim()
    .min(1, errorMessages["Attendance.GuestNameRequired"])
    .max(guestNameMaxLength, errorMessages["Attendance.GuestNameTooLong"]),
});

type GuestNameValues = z.infer<typeof guestNameSchema>;

interface GuestNameFormProps {
  /** Sends the trimmed name; a rejection is shown under the field or above the buttons. */
  onSubmit: (guestName: string) => Promise<void>;
  onBack: () => void;
}

/**
 * The one thing asked about a guest: their full name (BUSINESS_RULES.md §7 *Guest visit*). No
 * phone and no member record; the same person coming again is typed again.
 */
export function GuestNameForm({ onSubmit, onBack }: GuestNameFormProps) {
  const form = useForm<GuestNameValues>({
    resolver: zodResolver(guestNameSchema),
    defaultValues: { guestName: "" },
  });

  const submit = form.handleSubmit(async (values) => {
    try {
      await onSubmit(values.guestName);
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="space-y-4" onSubmit={submit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}
      <FormField
        label="نام و نام خانوادگی مهمان"
        autoComplete="off"
        autoFocus
        maxLength={guestNameMaxLength}
        error={errors.guestName?.message}
        {...form.register("guestName")}
      />
      <div className="flex gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "در حال ثبت…" : "ثبت ورود مهمان"}
        </Button>
        <Button type="button" variant="ghost" onClick={onBack}>
          بازگشت
        </Button>
      </div>
    </form>
  );
}
