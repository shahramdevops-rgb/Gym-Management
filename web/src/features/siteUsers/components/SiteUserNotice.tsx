import { Globe } from "lucide-react";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import type { Member } from "@/features/members/api";
import { toneButton } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

import { useRegisterSiteUser, useSiteUserByPhone } from "../api";

interface SiteUserNoticeProps {
  /** The phone box as typed. */
  phoneNumber: string;
  /** The person is now a member; the caller opens their profile, where a plan is sold. */
  onRegistered: (member: Member) => void;
}

/**
 * Under the new-member form's phone (BUSINESS_RULES.md §16 *Site users*): a whole number that
 * signed up on the site says so, with the name and birth date they entered, and «ثبت به‌عنوان
 * عضو» brings them in, so nobody is registered twice. What they entered is kept; the desk
 * corrects it on the profile if it needs to (the developer, 1405/08/19). Nothing is shown for any
 * other number.
 */
export function SiteUserNotice({ phoneNumber, onRegistered }: SiteUserNoticeProps) {
  const siteUser = useSiteUserByPhone(phoneNumber);
  const register = useRegisterSiteUser();

  if (!siteUser.isSuccess || siteUser.data === null) {
    return null;
  }

  const person = siteUser.data;

  return (
    <Alert role="status" className="grid gap-3">
      <p className="flex items-center gap-2 font-medium">
        <Globe className="size-4 shrink-0" aria-hidden />
        این شماره در سایت ثبت‌نام کرده است
      </p>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
        <dt className="text-muted-foreground">نام</dt>
        <dd>{person.fullName}</dd>
        <dt className="text-muted-foreground">تاریخ تولد</dt>
        <dd>{formatDate(person.birthDate)}</dd>
      </dl>
      {register.isError && (
        <p className="text-sm text-destructive">{errorMessage(register.error)}</p>
      )}
      <div>
        <Button
          type="button"
          variant="ghost"
          className={toneButton}
          disabled={register.isPending}
          onClick={() => register.mutate(person.id, { onSuccess: onRegistered })}
        >
          {register.isPending ? "در حال ثبت…" : "ثبت به‌عنوان عضو"}
        </Button>
      </div>
    </Alert>
  );
}
