import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";

import { DialogSuccess } from "@/components/DialogSuccess";
import { FormField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { dialogActionsClassName } from "@/components/ui/dialogActions";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { normalizeDigits, normalizePersianText } from "@/lib/normalize";
import { cn } from "@/lib/utils";

import { useCreateTrainer, useUpdateTrainer, type Trainer, type TrainerInput } from "../api";
import { trainerSchema, type TrainerValues } from "../schemas";

/**
 * Errors the API reports for the whole request that are really about one field: the phone needs
 * libphonenumber and its uniqueness the database, so only the handler can tell.
 */
const codeFields = {
  "Trainers.PhoneAlreadyExists": "phoneNumber",
  "Members.PhoneInvalid": "phoneNumber",
  "Members.PhoneNotMobile": "phoneNumber",
  "Members.PhoneNotIranian": "phoneNumber",
  "Members.PhoneRequired": "phoneNumber",
  "Trainers.FullNameRequired": "fullName",
  "Trainers.FullNameTooLong": "fullName",
} as const;

/** Values are normalized before they are sent, never while typing, as the member's form does. */
function toInput(values: TrainerValues): TrainerInput {
  return {
    fullName: normalizePersianText(values.fullName),
    phoneNumber: normalizeDigits(values.phoneNumber).trim(),
  };
}

/**
 * A mobile's own ten digits (`9121234567`), whichever way it is written: the saved `+98912…`, a
 * typed `0912 …` or Persian digits. Only for the hint below; the server decides what the phone is.
 */
function lastTenDigits(phone: string): string {
  return normalizeDigits(phone).replace(/\D/g, "").slice(-10);
}

interface TrainerFormDialogProps {
  open: boolean;
  /** The trainer being corrected; without one, a new trainer is made. */
  trainer?: Trainer;
  onClose: () => void;
}

/**
 * «افزودن مربی» and «ویرایش» (BUSINESS_RULES.md §15 *Trainers*): a name and a phone. The trainer
 * then signs in on the site with that phone. A new phone ends the old one's site sessions, so the
 * form says so as soon as the phone is changed.
 */
export function TrainerFormDialog({ open, trainer, onClose }: TrainerFormDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      {/* Mounted only while open, so every opening starts from the trainer's saved values. */}
      {open && <TrainerFormContent trainer={trainer} onClose={onClose} />}
    </Dialog>
  );
}

function TrainerFormContent({ trainer, onClose }: Omit<TrainerFormDialogProps, "open">) {
  const create = useCreateTrainer();
  const update = useUpdateTrainer();
  const [saved, setSaved] = useState<Trainer | null>(null);

  const form = useForm<TrainerValues>({
    resolver: zodResolver(trainerSchema),
    defaultValues: {
      fullName: trainer?.fullName ?? "",
      phoneNumber: trainer?.phoneNumber ?? "",
    },
  });

  const phoneNumber = useWatch({ control: form.control, name: "phoneNumber" });
  const phoneChanged =
    trainer !== undefined && lastTenDigits(phoneNumber) !== lastTenDigits(trainer.phoneNumber);

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      const input = toInput(values);
      setSaved(
        trainer === undefined
          ? await create.mutateAsync(input)
          : await update.mutateAsync({ id: trainer.id, version: trainer.version, ...input }),
      );
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  if (saved !== null) {
    return (
      <DialogContent>
        <DialogSuccess
          title={trainer === undefined ? "مربی ثبت شد" : "تغییرات مربی ذخیره شد"}
          description={
            trainer === undefined
              ? `«${saved.fullName}» حالا می‌تواند با همین شماره موبایل وارد سایت شود.`
              : saved.fullName
          }
          onClose={onClose}
        />
      </DialogContent>
    );
  }

  return (
    <DialogContent fullScreenOnPhone>
      <DialogHeader>
        <DialogTitle>{trainer === undefined ? "افزودن مربی" : "ویرایش مربی"}</DialogTitle>
        <DialogDescription>
          مربی با همین شماره موبایل و کد پیامکی وارد سایت می‌شود.
        </DialogDescription>
      </DialogHeader>

      <form className="space-y-4" onSubmit={onSubmit} noValidate>
        {errors.root?.server !== undefined && (
          <Alert variant="destructive">{errors.root.server.message}</Alert>
        )}

        <FormField
          label="نام و نام خانوادگی"
          autoComplete="off"
          error={errors.fullName?.message}
          {...form.register("fullName")}
        />
        <div className="space-y-2">
          <FormField
            label="شماره موبایل"
            dir="ltr"
            inputMode="tel"
            autoComplete="off"
            placeholder="۰۹۱۲ ۱۲۳ ۴۵۶۷"
            error={errors.phoneNumber?.message}
            {...form.register("phoneNumber")}
          />
          {phoneChanged && (
            <p className="text-sm text-muted-foreground">
              با شمارهٔ تازه، مربی از سایت خارج می‌شود و باید با شمارهٔ تازه دوباره وارد شود.
            </p>
          )}
        </div>

        <div className={cn("flex flex-wrap gap-2", dialogActionsClassName)}>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "در حال ذخیره…" : trainer === undefined ? "ثبت مربی" : "ذخیره"}
          </Button>
          <Button type="button" variant="outline" disabled={isSubmitting} onClick={onClose}>
            انصراف
          </Button>
        </div>
      </form>
    </DialogContent>
  );
}
