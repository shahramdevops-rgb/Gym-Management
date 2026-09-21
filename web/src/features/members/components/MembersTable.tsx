import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatPhone } from "@/lib/format";

import type { Member } from "../api";

interface MembersTableProps {
  members: Member[];
  /**
   * One-click check-in (docs/ROADMAP.md 5.6), shown only where a caller passes it — the front
   * desk search, not the full member directory. `checkingInId` disables only the row in flight.
   */
  onCheckIn?: (member: Member) => void;
  checkingInId?: string | null;
}

/** Search results and the member list: name (a link to the profile), phone, status. */
export function MembersTable({ members, onCheckIn, checkingInId = null }: MembersTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">نام</th>
            <th className="py-2 text-start font-medium">موبایل</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            {onCheckIn !== undefined && (
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
              {onCheckIn !== undefined && (
                <td className="py-2">
                  <div className="flex justify-end">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={checkingInId === member.id}
                      onClick={() => onCheckIn(member)}
                    >
                      ورود
                    </Button>
                  </div>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
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
