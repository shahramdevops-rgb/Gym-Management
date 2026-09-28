import { Pencil } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";

import { paths } from "@/app/paths";
import { PageMessage } from "@/features/auth/components/PageMessage";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Pager } from "@/components/Pager";
import { useMemberAttendanceHistory } from "@/features/attendance/api";
import { AttendanceHistoryTable } from "@/features/attendance/components/AttendanceHistoryTable";
import { useDeskDialog } from "@/features/attendance/components/useDeskDialog";
import { CurrentSubscriptionCard } from "@/features/subscriptions/components/CurrentSubscriptionCard";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatDateTime, toPersianDigits } from "@/lib/format";

import { useMember, useSetMemberActive } from "../api";
import { MemberDebtCard } from "../components/MemberDebtCard";
import { MemberHistoryTabs } from "../components/MemberHistoryTabs";
import { MemberStatusBadge, PhoneNumber } from "../components/MembersTable";

/**
 * A member's basic details, current subscription (task 4.6), attendance (task 5.6) and the
 * subscription, payment and cafe purchase history (task 7.4).
 *
 * Deactivating asks for no confirmation: it deletes nothing and "فعال‌سازی" undoes it in one
 * click (decided in task 2.3).
 */
export function MemberProfilePage() {
  const { id = "" } = useParams();
  const member = useMember(id);
  const setActive = useSetMemberActive();
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  const [historyPage, setHistoryPage] = useState(1);
  const history = useMemberAttendanceHistory(id, historyPage);
  // Check-out and cancelling a check-in ask first and show the outcome in a box, the same as on
  // the locker map (BUSINESS_RULES.md §7 Confirming at the front desk).
  const desk = useDeskDialog();

  if (member.isPending) {
    return <PageMessage>در حال بارگذاری…</PageMessage>;
  }

  if (member.isError) {
    return <PageMessage>{errorMessage(member.error)}</PageMessage>;
  }

  const current = member.data;

  // The most recent visit, first page of the history sorted newest first, is the only one that
  // can still be open — check-in refuses a second one while any attendance has no checkedOutAt.
  const latest = historyPage === 1 ? history.data?.items[0] : undefined;
  const openAttendance = latest !== undefined && latest.checkedOutAt === null ? latest : undefined;

  async function toggleActive() {
    setNotice(null);
    try {
      await setActive.mutateAsync({ id, active: !current.isActive });
      setNotice({
        kind: "success",
        text: current.isActive ? "عضو غیرفعال شد." : "عضو دوباره فعال شد.",
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  return (
    <div className="max-w-4xl space-y-4">
      {notice !== null && (
        <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
          {notice.text}
        </Alert>
      )}

      <Card>
        <CardHeader className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <CardTitle className="text-xl">{current.fullName}</CardTitle>
            <MemberStatusBadge isActive={current.isActive} />
          </div>
          <div className="flex gap-2">
            <Button asChild size="sm" variant="outline">
              <Link to={paths.editMember(id)}>
                <Pencil aria-hidden />
                ویرایش
              </Link>
            </Button>
            <Button
              size="sm"
              variant={current.isActive ? "destructive" : "secondary"}
              disabled={setActive.isPending}
              onClick={() => void toggleActive()}
            >
              {current.isActive ? "غیرفعال‌سازی" : "فعال‌سازی"}
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-[max-content_1fr]">
            <dt className="text-muted-foreground">شماره موبایل</dt>
            <dd>
              <PhoneNumber value={current.phoneNumber} />
            </dd>

            <dt className="text-muted-foreground">تاریخ تولد</dt>
            <dd>{formatDate(current.birthDate)}</dd>

            <dt className="text-muted-foreground">یادداشت</dt>
            <dd className="whitespace-pre-line">{current.notes ?? "—"}</dd>

            <dt className="text-muted-foreground">تاریخ ثبت</dt>
            <dd>{formatDateTime(current.createdAt)}</dd>
          </dl>
        </CardContent>
      </Card>

      {!current.isActive && (
        <p className="text-sm text-muted-foreground">
          عضو غیرفعال نمی‌تواند وارد باشگاه شود یا اشتراک تازه بگیرد.
        </p>
      )}

      <MemberDebtCard memberId={id} />

      <CurrentSubscriptionCard memberId={id} />

      <Card>
        <CardHeader>
          <CardTitle>ورود و خروج</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {history.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {history.isError && <Alert variant="destructive">{errorMessage(history.error)}</Alert>}

          {history.isSuccess &&
            (openAttendance !== undefined ? (
              <div className="space-y-3 rounded-md border p-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div className="text-sm">
                    <p className="font-medium">هم‌اکنون داخل باشگاه است.</p>
                    <p className="text-muted-foreground">
                      ورود: {formatDateTime(openAttendance.checkedInAt)}
                      {openAttendance.lockerNumber !== null
                        ? ` · کمد ${toPersianDigits(openAttendance.lockerNumber)}`
                        : openAttendance.usesReservePlace && " · بدون کمد"}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() =>
                        desk.open({
                          kind: "cancelCheckIn",
                          member: current,
                          attendanceId: openAttendance.id,
                        })
                      }
                    >
                      لغو ورود
                    </Button>
                    <Button
                      size="sm"
                      onClick={() =>
                        desk.open({
                          kind: "checkOut",
                          member: current,
                          visit: {
                            attendanceId: openAttendance.id,
                            lockerNumber: openAttendance.lockerNumber,
                          },
                        })
                      }
                    >
                      ثبت خروج
                    </Button>
                  </div>
                </div>
              </div>
            ) : (
              // No check-in button here since roadmap 6.5.5: the desk lets a member in from the
              // locker map, by clicking the locker it gives them (BUSINESS_RULES.md §7).
              <p className="text-sm text-muted-foreground">
                برای ثبت ورود، کمد را در صفحهٔ{" "}
                <Link
                  to={paths.home}
                  className="font-medium text-foreground underline-offset-4 hover:underline"
                >
                  ورود با کمد
                </Link>{" "}
                انتخاب کنید.
              </p>
            ))}

          {history.isSuccess && (
            <div className="space-y-3">
              <h3 className="text-sm font-medium text-muted-foreground">تاریخچه ورود و خروج</h3>
              {history.data.items.length === 0 ? (
                <p className="text-muted-foreground">هنوز ورودی ثبت نشده است.</p>
              ) : (
                <>
                  <AttendanceHistoryTable items={history.data.items} />
                  <Pager
                    page={historyPage}
                    pageCount={history.data.pageCount}
                    onPageChange={setHistoryPage}
                  />
                </>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <MemberHistoryTabs memberId={id} />

      {desk.dialog}
    </div>
  );
}
