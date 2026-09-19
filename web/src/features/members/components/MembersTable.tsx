import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { formatPhone } from "@/lib/format";

import type { Member } from "../api";

/** Search results and the member list: name (a link to the profile), phone, status. */
export function MembersTable({ members }: { members: Member[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">نام</th>
            <th className="py-2 text-start font-medium">موبایل</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">پرداخت</th>
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
                {member.hasUnpaidSubscription && <Badge variant="destructive">بدهکار</Badge>}
              </td>
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
