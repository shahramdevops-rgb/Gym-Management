import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { normalizePassword } from "@/features/auth/schemas";
import { applyServerErrors, zodResolver } from "@/lib/forms";

import { useResetStaffPassword, type StaffMember } from "../api";
import { resetPasswordSchema, type ResetPasswordValues } from "../schemas";

interface ResetPasswordFormProps {
  staff: StaffMember;
  onDone: (message: string) => void;
  onCancel: () => void;
}

/** Opens under a staff row: the Owner types a new temporary password for that person. */
export function ResetPasswordForm({ staff, onDone, onCancel }: ResetPasswordFormProps) {
  const resetPassword = useResetStaffPassword();

  const form = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { temporaryPassword: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await resetPassword.mutateAsync({
        id: staff.id,
        temporaryPassword: normalizePassword(values.temporaryPassword),
      });
      onDone(`رمز عبور «${staff.fullName}» بازنشانی شد. او در ورود بعدی باید آن را تغییر دهد.`);
    } catch (problem) {
      applyServerErrors(problem, form.setError);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="flex flex-wrap items-end gap-3" onSubmit={onSubmit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}
      <div className="min-w-64 flex-1">
        <FormField
          label={`رمز عبور موقت تازه برای ${staff.fullName}`}
          dir="ltr"
          autoComplete="new-password"
          error={errors.temporaryPassword?.message}
          {...form.register("temporaryPassword")}
        />
      </div>
      <Button type="submit" size="sm" disabled={isSubmitting}>
        ذخیره
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
