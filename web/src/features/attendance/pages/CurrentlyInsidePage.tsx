import { useState } from "react";
import { useSearchParams } from "react-router";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { pageFromParams } from "@/lib/searchParams";

import { useCancelCheckIn, useCheckOut, useCurrentlyInside } from "../api";
import { CurrentlyInsideTable } from "../components/CurrentlyInsideTable";

/**
 * The front desk board: everyone inside the gym right now, refreshing on its own
 * (docs/ROADMAP.md 5.6) so staff do not have to reload it during a shift.
 */
export function CurrentlyInsidePage() {
  const [params, setParams] = useSearchParams();
  const page = pageFromParams(params);
  const inside = useCurrentlyInside(page);
  const checkOut = useCheckOut();
  const cancel = useCancelCheckIn();
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  async function handleCheckOut(attendanceId: string) {
    setNotice(null);
    setPendingId(attendanceId);
    try {
      await checkOut.mutateAsync(attendanceId);
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    } finally {
      setPendingId(null);
    }
  }

  async function handleCancel(attendanceId: string) {
    setNotice(null);
    setPendingId(attendanceId);
    try {
      await cancel.mutateAsync(attendanceId);
      setNotice({ kind: "success", text: "ورود لغو شد و جلسه به اشتراک بازگشت." });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    } finally {
      setPendingId(null);
    }
  }

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
          {notice !== null && (
            <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
              {notice.text}
            </Alert>
          )}

          {inside.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {inside.isError && <Alert variant="destructive">{errorMessage(inside.error)}</Alert>}

          {inside.isSuccess && inside.data.items.length === 0 && (
            <p className="text-muted-foreground">در حال حاضر کسی داخل باشگاه نیست.</p>
          )}

          {inside.isSuccess && inside.data.items.length > 0 && (
            <>
              <CurrentlyInsideTable
                rows={inside.data.items}
                busyAttendanceId={pendingId}
                onCheckOut={(id) => void handleCheckOut(id)}
                onCancel={(id) => void handleCancel(id)}
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
    </div>
  );
}
