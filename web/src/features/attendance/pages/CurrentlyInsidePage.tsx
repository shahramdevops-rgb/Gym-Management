import { useSearchParams } from "react-router";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { pageFromParams } from "@/lib/searchParams";

import { useCurrentlyInside } from "../api";
import { CurrentlyInsideTable } from "../components/CurrentlyInsideTable";
import { useDeskDialog } from "../components/useDeskDialog";

/**
 * The front desk board: everyone inside the gym right now, refreshing on its own
 * (docs/ROADMAP.md 5.6) so staff do not have to reload it during a shift.
 */
export function CurrentlyInsidePage() {
  const [params, setParams] = useSearchParams();
  const page = pageFromParams(params);
  const inside = useCurrentlyInside(page);
  // Check-out and cancelling a check-in ask first, in the same box as the entry screen
  // (BUSINESS_RULES.md §7 Confirming at the front desk); check-out also reminds the desk about the
  // key and shows the debt.
  const desk = useDeskDialog();

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-bold">داخل باشگاه</h2>

      <Card>
        <CardHeader>
          <CardTitle>
            حاضرین
            {inside.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(inside.data.totalCount)})
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {inside.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {inside.isError && <Alert variant="destructive">{errorMessage(inside.error)}</Alert>}

          {inside.isSuccess && inside.data.items.length === 0 && (
            <p className="text-muted-foreground">در حال حاضر کسی داخل باشگاه نیست.</p>
          )}

          {inside.isSuccess && inside.data.items.length > 0 && (
            <>
              <CurrentlyInsideTable
                rows={inside.data.items}
                onCheckOut={(row) =>
                  desk.open({
                    kind: "checkOut",
                    member: { id: row.memberId, fullName: row.memberFullName },
                    visit: { attendanceId: row.attendanceId, lockerNumber: row.lockerNumber },
                  })
                }
                onCancel={(row) =>
                  desk.open({
                    kind: "cancelCheckIn",
                    member: { id: row.memberId, fullName: row.memberFullName },
                    attendanceId: row.attendanceId,
                  })
                }
              />
              <Pager
                page={page}
                pageCount={inside.data.pageCount}
                onPageChange={(next) => setParams({ page: String(next) })}
              />
            </>
          )}
        </CardContent>
      </Card>

      {desk.dialog}
    </div>
  );
}
