import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatMoney, formatPhone } from "@/lib/format";
import { isPositiveMoney } from "@/lib/money";

import type { Member } from "../api";

interface MembersTableProps {
  members: Member[];
  /**
   * Check-in and check-out from the row (docs/ROADMAP.md 5.6), shown only where a caller passes
   * them — the front desk search, not the full member directory. Each opens a confirmation first
   * (BUSINESS_RULES.md §7 *Confirming at the front desk*); the row only says which was pressed.
   */
  deskActions?: {
    onCheckIn: (member: Member) => void;
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
                <MemberStatusBadge isActive={member.isActive} />
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
 * Whichever of check-in and check-out makes sense is the live button; the other stays visible but
 * grey, so the row keeps its shape and the desk sees at a glance which state the member is in.
 * Inside is decided by the open visit the API reports, the same test check-in refuses on
 * (BUSINESS_RULES.md §7).
 */
function DeskButtons({
  member,
  onCheckIn,
  onCheckOut,
}: {
  member: Member;
  onCheckIn: (member: Member) => void;
  onCheckOut: (member: Member) => void;
}) {
  const inside = (member.currentVisit ?? null) !== null;

  return (
    <div className="flex items-center justify-end gap-2">
      {inside && <Badge variant="secondary">داخل باشگاه</Badge>}
      <Button size="sm" variant="outline" disabled={inside} onClick={() => onCheckIn(member)}>
        ورود
      </Button>
      <Button size="sm" variant="outline" disabled={!inside} onClick={() => onCheckOut(member)}>
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
