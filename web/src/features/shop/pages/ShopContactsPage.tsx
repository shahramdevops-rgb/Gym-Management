import { useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";

import { FormField } from "@/components/FormField";
import { PageHeader } from "@/components/PageHeader";
import { SaveRow, StaleAlert, type SaveNotice } from "@/components/SaveRow";
import { LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { useRoles } from "@/features/auth/api";
import { errorMessage } from "@/lib/errors";
import { formatPhone, toPersianDigits } from "@/lib/format";
import { applyServerErrors, useUnchanged, zodResolver } from "@/lib/forms";
import { normalizeDigits } from "@/lib/normalize";

import { useShopContacts, useUpdateShopContacts, type ShopContacts } from "../api";
import {
  cleanHandle,
  cleanLandline,
  shopContactsSchema,
  type ShopContactsValues,
} from "../schemas";

const changedConcurrently = "Shop.ChangedConcurrently";

function isCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}

type Way = keyof ShopContactsValues;

/** The eight ways, in the order the form and the Owner's list show them (decided 1405/08/20). */
const ways: readonly { name: Way; label: string; placeholder: string; kind: "phone" | "handle" }[] =
  [
    { name: "mobilePhone", label: "تلفن همراه", placeholder: "۰۹۱۲ ۱۲۳ ۴۵۶۷", kind: "phone" },
    { name: "landlinePhone", label: "تلفن ثابت", placeholder: "۰۷۱۳۲۳۴۵۶۷۸", kind: "phone" },
    { name: "whatsAppPhone", label: "واتساپ", placeholder: "۰۹۱۲ ۱۲۳ ۴۵۶۷", kind: "phone" },
    { name: "telegram", label: "تلگرام", placeholder: "pasargad_gym", kind: "handle" },
    { name: "rubika", label: "روبیکا", placeholder: "pasargadgym", kind: "handle" },
    { name: "eitaa", label: "ایتا", placeholder: "pasargadgym", kind: "handle" },
    { name: "bale", label: "بله", placeholder: "pasargadgym", kind: "handle" },
    { name: "instagram", label: "اینستاگرام", placeholder: "pasargad.gym", kind: "handle" },
  ];

/** A saved way as the screen shows it: a mobile as people write it, the rest with Persian digits. */
function shown(name: Way, value: string | null): string {
  if (value === null) {
    return "";
  }
  if (name === "mobilePhone" || name === "whatsAppPhone") {
    return formatPhone(value);
  }
  return name === "landlinePhone" ? toPersianDigits(value) : value;
}

/**
 * «راه‌های تماس» (BUSINESS_RULES.md §16 *The shop admin*): the ways a customer can reach the shop,
 * one set for the whole shop. The shop admin edits them; the Owner reads them and cannot change
 * them (the developer, 1405/08/20). Staff never reach the page (RequireRole). Every way is
 * optional, and an empty one will not be shown to customers.
 */
export function ShopContactsPage() {
  const contacts = useShopContacts();
  const roles = useRoles();
  const [stale, setStale] = useState(false);
  const [reloading, setReloading] = useState(false);
  const [notice, setNotice] = useState<SaveNotice>(null);

  if (contacts.isPending) {
    return <LoadingState />;
  }

  if (contacts.isError) {
    return <Alert variant="destructive">{errorMessage(contacts.error)}</Alert>;
  }

  async function reload() {
    setReloading(true);
    await contacts.refetch();
    setReloading(false);
    setStale(false);
  }

  // From the token until «me» answers, so a failed «me» never shows the shop admin the Owner's view.
  const canEdit = roles.includes("Shop");

  return (
    <div className="max-w-3xl space-y-6">
      <PageHeader
        title="راه‌های تماس فروشگاه"
        description="مشتری‌ها از این راه‌ها با فروشگاه تماس می‌گیرند. هر راهی که خالی بماند به آن‌ها نشان داده نمی‌شود."
      />
      <Card>
        <CardContent>
          {canEdit ? (
            // Keyed by version, so fresh values rebuild the form with them.
            <ContactsForm
              key={String(contacts.data.version)}
              current={contacts.data}
              notice={notice}
              onNotice={setNotice}
              onStale={() => setStale(true)}
              stale={
                stale ? (
                  <StaleAlert
                    code={changedConcurrently}
                    reloading={reloading}
                    onReload={() => void reload()}
                  />
                ) : undefined
              }
            />
          ) : (
            <ContactsList current={contacts.data} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

/** The Owner's view: the ways as they are, to read. */
function ContactsList({ current }: { current: ShopContacts }) {
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">این‌ها را فقط مدیر فروشگاه تغییر می‌دهد.</p>
      <dl className="grid gap-3 sm:grid-cols-2">
        {ways.map((way) => {
          const value = shown(way.name, current[way.name]);
          return (
            <div key={way.name} className="rounded-lg border px-3 py-2">
              <dt className="text-xs text-muted-foreground">{way.label}</dt>
              <dd className="mt-0.5 text-sm font-medium">
                {value === "" ? (
                  <span className="text-muted-foreground">ثبت نشده</span>
                ) : (
                  <span dir="ltr">{value}</span>
                )}
              </dd>
            </div>
          );
        })}
      </dl>
    </div>
  );
}

interface ContactsFormProps {
  current: ShopContacts;
  notice: SaveNotice;
  onNotice: (notice: SaveNotice) => void;
  onStale: () => void;
  stale: ReactNode;
}

/** The request these values make: what a save sends, and what "nothing changed" compares. */
function toInput(values: ShopContactsValues, version: ShopContacts["version"]) {
  const phone = (typed: string) => {
    const digits = normalizeDigits(typed).trim();
    return digits === "" ? null : digits;
  };
  const text = (cleaned: string) => (cleaned === "" ? null : cleaned);

  return {
    mobilePhone: phone(values.mobilePhone),
    landlinePhone: text(cleanLandline(values.landlinePhone)),
    whatsAppPhone: phone(values.whatsAppPhone),
    telegram: text(cleanHandle(values.telegram)),
    rubika: text(cleanHandle(values.rubika)),
    eitaa: text(cleanHandle(values.eitaa)),
    bale: text(cleanHandle(values.bale)),
    instagram: text(cleanHandle(values.instagram)),
    version,
  };
}

/** The shop admin's form: eight small boxes, «ذخیره» under them, as every edit form. */
function ContactsForm({ current, notice, onNotice, onStale, stale }: ContactsFormProps) {
  const updateContacts = useUpdateShopContacts();
  const defaultValues: ShopContactsValues = {
    mobilePhone: shown("mobilePhone", current.mobilePhone),
    landlinePhone: shown("landlinePhone", current.landlinePhone),
    whatsAppPhone: shown("whatsAppPhone", current.whatsAppPhone),
    telegram: current.telegram ?? "",
    rubika: current.rubika ?? "",
    eitaa: current.eitaa ?? "",
    bale: current.bale ?? "",
    instagram: current.instagram ?? "",
  };
  const isUnchanged = useUnchanged(() => toInput(defaultValues, current.version));
  const form = useForm<ShopContactsValues>({
    resolver: zodResolver(shopContactsSchema),
    defaultValues,
  });

  const submit = form.handleSubmit(async (values) => {
    const input = toInput(values, current.version);
    if (isUnchanged(input)) {
      onNotice("unchanged");
      return;
    }

    onNotice(null);
    try {
      await updateContacts.mutateAsync(input);
      onNotice("saved");
    } catch (problem) {
      if (isCode(problem, changedConcurrently)) {
        onStale();
        return;
      }
      applyServerErrors(problem, form.setError, {
        "Shop.MobilePhoneInvalid": "mobilePhone",
        "Shop.WhatsAppPhoneInvalid": "whatsAppPhone",
      });
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form
      className="grid gap-4 sm:grid-cols-2"
      onSubmit={submit}
      onChange={() => onNotice(null)}
      noValidate
    >
      {ways.map((way) => (
        <FormField
          key={way.name}
          label={way.label}
          dir="ltr"
          inputMode={way.kind === "phone" ? "tel" : "text"}
          autoComplete="off"
          placeholder={way.placeholder}
          error={errors[way.name]?.message}
          {...form.register(way.name)}
        />
      ))}

      <div className="sm:col-span-2">
        <SaveRow error={errors.root?.server?.message} notice={notice} stale={stale}>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "در حال ذخیره…" : "ذخیره"}
          </Button>
        </SaveRow>
      </div>
    </form>
  );
}
