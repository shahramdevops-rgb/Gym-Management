import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatMoney, formatPhone, toPersianDigits } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";

import type { Member } from "../api";

interface MembersTableProps {
  members: Member[];
  /**
   * Check-out from the row of someone inside, shown only where a caller passes it (the member
   * list). It opens a confirmation first (BUSINESS_RULES.md §7 *Confirming at the front desk*).
   * There is no check-in here: that happens on the locker map, where the locker is chosen
   * (roadmap 6.5.5).
   */
  deskActions?: {
    onCheckOut: (member: Member) => void;
  };
}

/** Search results and the member list: name (a link to the profile), phone, status. */
export function MembersTable({ members, deskActions }: MembersTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">نام</th>
            <th className="py-2 text-start font-medium">موبایل</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">بدهی</th>
            {deskActions !== undefined && (
              <th className="py-2 text-start font-medium">
                <span className="sr-only">عملیات</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {members.map((member) => (
            <tr key={member.id} className="border-b">
              <td className="py-2">
                <Link
                  to={paths.member(member.id)}
                  className="font-medium underline-offset-4 hover:underline"
                >
                  {member.fullName}
                </Link>
              </td>
              <td className="py-2">
                <PhoneNumber value={member.phoneNumber} />
              </td>
              <td className="py-2">
                <div className="flex flex-wrap items-center gap-1">
                  <MemberStatusBadge isActive={member.isActive} />
                  {/* Only the list fills isFrozen, so it is shown here, not in MemberStatusBadge. */}
                  {member.isFrozen && <Badge variant="secondary">فریز</Badge>}
                </div>
              </td>
              <td className="py-2">
                <MemberDebt value={member.debt} />
              </td>
              {deskActions !== undefined && (
                <td className="py-2">
                  <DeskButtons member={member} {...deskActions} />
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/**
 * For someone inside, where they are and the check-out button; nothing for anyone else. Inside is
 * decided by the open visit the API reports, the same test check-in refuses on (BUSINESS_RULES.md
 * §7).
 */
function DeskButtons({
  member,
  onCheckOut,
}: {
  member: Member;
  onCheckOut: (member: Member) => void;
}) {
  const visit = member.currentVisit ?? null;
  if (visit === null) {
    return null;
  }

  return (
    <div className="flex items-center justify-end gap-2">
      <Badge variant="secondary">
        داخل باشگاه —{" "}
        {visit.lockerNumber === null ? "بدون کمد" : `کمد ${toPersianDigits(visit.lockerNumber)}`}
      </Badge>
      <Button size="sm" variant="outline" onClick={() => onCheckOut(member)}>
        خروج
      </Button>
    </div>
  );
}

/**
 * The amount rather than a "بدهکار" badge (task 4.7): the gym runs open accounts, so whether a
 * member owes anything matters less than how much, and the front desk reads it off the list
 * before opening the profile (BUSINESS_RULES.md §5 Member debt).
 */
function MemberDebt({ value }: { value: Member["debt"] }) {
  return isPositiveMoney(value) ? <Badge variant="destructive">{formatMoney(value)}</Badge> : null;
}

export function MemberStatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <Badge variant={isActive ? "success" : "secondary"}>{isActive ? "فعال" : "غیرفعال"}</Badge>
  );
}

/**
 * A phone number laid out left to right. Without dir="ltr" the right-to-left page would put
 * the three digit groups in reverse order. `inline-block` keeps it aligned with the column's
 * start, like the text around it.
 */
export function PhoneNumber({ value }: { value: string }) {
  return (
    <span dir="ltr" className="inline-block">
      {formatPhone(value)}
    </span>
  );
}
