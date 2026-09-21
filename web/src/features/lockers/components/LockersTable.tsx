import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toPersianDigits } from "@/lib/format";

import type { Locker } from "../api";

interface LockersTableProps {
  lockers: Locker[];
  /** Disables the out-of-service/in-service buttons while a change is being saved. */
  busy: boolean;
  onToggleOutOfService: (locker: Locker) => void;
}

/**
 * The Owner's locker list: number, live occupancy, and whether it is in service.
 * A locker in use is shown occupied but the out-of-service button is left enabled — the API is
 * the one place that rule is enforced (BUSINESS_RULES.md §6), and a click that is refused just
 * shows the server's reason, the same "attempt, then explain" pattern as everywhere else.
 */
export function LockersTable({ lockers, busy, onToggleOutOfService }: LockersTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">شماره</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">
              <span className="sr-only">عملیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {lockers.map((locker) => (
            <tr key={locker.id} className="border-b">
              <td className="py-2 font-medium">{toPersianDigits(locker.number)}</td>
              <td className="py-2">
                <LockerStatusBadge locker={locker} />
              </td>
              <td className="py-2">
                <div className="flex justify-end">
                  <Button
                    size="sm"
                    variant={locker.isOutOfService ? "secondary" : "outline"}
                    disabled={busy}
                    onClick={() => onToggleOutOfService(locker)}
                  >
                    {locker.isOutOfService ? "بازگرداندن به سرویس" : "خارج از سرویس"}
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function LockerStatusBadge({ locker }: { locker: Locker }) {
  if (locker.isOutOfService) {
    return <Badge variant="secondary">خارج از سرویس</Badge>;
  }

  return locker.isOccupied ? (
    <Badge variant="destructive">اشغال</Badge>
  ) : (
    <Badge variant="success">آزاد</Badge>
  );
}
