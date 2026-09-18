import { useState } from "react";
import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { normalizePassword } from "@/features/auth/schemas";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeDigits, normalizePersianText } from "@/lib/normalize";

import { useCreateStaff } from "../api";
import { createStaffSchema, type CreateStaffValues } from "../schemas";

export function CreateStaffForm() {
  const createStaff = useCreateStaff();
  const [created, setCreated] = useState<string | null>(null);

  const form = useForm<CreateStaffValues>({
    resolver: zodResolver(createStaffSchema),
    defaultValues: { userName: "", fullName: "", temporaryPassword: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setCreated(null);
    try {
      const staff = await createStaff.mutateAsync({
        userName: normalizeDigits(values.userName).trim(),
        fullName: normalizePersianText(values.fullName),
        temporaryPassword: normalizePassword(values.temporaryPassword),
      });
      setCreated(staff.fullName);
      form.reset();
    } catch (problem) {
      applyServerErrors(problem, form.setError, { "Staff.UserNameTaken": "userName" });
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <Card>
      <CardHeader>
        <CardTitle>کارمند جدید</CardTitle>
        <CardDescription>
          رمز عبور موقت را به کارمند بدهید. او در اولین ورود باید آن را تغییر دهد.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="grid gap-4 md:grid-cols-3" onSubmit={onSubmit} noValidate>
          {created !== null && (
            <Alert variant="success" role="status" className="md:col-span-3">
              حساب «{created}» ساخته شد.
            </Alert>
          )}
          {errors.root?.server !== undefined && (
            <Alert variant="destructive" className="md:col-span-3">
              {errors.root.server.message}
            </Alert>
          )}

          <FormField
            label="نام و نام خانوادگی"
            error={errors.fullName?.message}
            {...form.register("fullName")}
          />
          <FormField
            label="نام کاربری"
            dir="ltr"
            autoComplete="off"
            error={errors.userName?.message}
            {...form.register("userName")}
          />
          <FormField
            label="رمز عبور موقت"
            dir="ltr"
            autoComplete="new-password"
            error={errors.temporaryPassword?.message}
            {...form.register("temporaryPassword")}
          />

          <div className="md:col-span-3">
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "در حال ساخت…" : "ساخت حساب"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
