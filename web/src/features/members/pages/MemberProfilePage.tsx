import { Footprints, Pencil } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";

import { paths } from "@/app/paths";
import { PageHero } from "@/components/PageHero";
import { EmptyState, LoadingState } from "@/components/States";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Pager } from "@/components/Pager";
import { useMemberAttendanceHistory } from "@/features/attendance/api";
import { AttendanceHistoryTable } from "@/features/attendance/components/AttendanceHistoryTable";
import { useDeskDialog } from "@/features/attendance/components/useDeskDialog";
import { CurrentSubscriptionCard } from "@/features/subscriptions/components/CurrentSubscriptionCard";
import { stateEdge, toneAction } from "@/lib/edges";
import { errorMessage } from "@/lib/errors";
import { formatDate, formatDateTime, toPersianDigits } from "@/lib/format";
import { cn } from "@/lib/utils";

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
    return <LoadingState />;
  }

  if (member.isError) {
    return <Alert variant="destructive">{errorMessage(member.error)}</Alert>;
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
    // As wide as the column, so the banner runs to the far end like every other page's (13.2j).
    <div className="space-y-4">
      {/* The member's name is the page's title, on the banner like the members' list (13.2j); the
          tag beside it says whether they may come in. «ویرایش» is edged in the tone like «عضو
          جدید», and deactivating in red like «لغو» on the cafe's orders, with no fill. */}
      <PageHero
        title={current.fullName}
        actions={
          <>
            <Button asChild variant="ghost" className={toneAction}>
              <Link to={paths.editMember(id)}>
                <Pencil aria-hidden />
                ویرایش
              </Link>
            </Button>
            <Button
              variant="ghost"
              className={cn(
                "h-10",
                current.isActive
                  ? cn(stateEdge.destructive, "hover:bg-destructive/10 hover:text-destructive")
                  : cn(stateEdge.success, "hover:bg-success/10 hover:text-success-ink"),
              )}
              disabled={setActive.isPending}
              onClick={() => void toggleActive()}
            >
              {current.isActive ? "غیرفعال‌سازی" : "فعال‌سازی"}
            </Button>
          </>
        }
      >
        <span className="inline-flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <MemberStatusBadge isActive={current.isActive} />
          {!current.isActive && (
            <span>عضو غیرفعال نمی‌تواند وارد باشگاه شود یا اشتراک تازه بگیرد.</span>
          )}
        </span>
      </PageHero>

      {notice !== null && (
        <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
          {notice.text}
        </Alert>
      )}

      {/* Two columns on a wide screen (13.2j, asked by the developer, so the room beside the
          details is used): the details and the debt at the start, the current subscription
          facing them. */}
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="space-y-4">
          <Card edge>
            <CardHeader>
              <CardTitle>مشخصات</CardTitle>
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

                {/* Whether they have signed in on the site (§15): made by their first sign-in. */}
                <dt className="text-muted-foreground">حساب سایت</dt>
                <dd>
                  {current.siteAccount == null ? (
                    <span className="text-muted-foreground">در سایت ثبت‌نام نکرده است.</span>
                  ) : (
                    <>
                      <p>ثبت‌نام: {formatDateTime(current.siteAccount.createdAt)}</p>
                      <p className="text-muted-foreground">
                        آخرین ورود: {formatDateTime(current.siteAccount.lastSignedInAt)}
                      </p>
                    </>
                  )}
                </dd>
              </dl>
            </CardContent>
          </Card>

          <MemberDebtCard memberId={id} />
        </div>

        <CurrentSubscriptionCard memberId={id} />
      </div>

      <Card edge>
        <CardHeader>
          <CardTitle>ورود و خروج</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {history.isPending && <LoadingState />}
          {history.isError && <Alert variant="destructive">{errorMessage(history.error)}</Alert>}

          {history.isSuccess &&
            (openAttendance !== undefined ? (
              // Edged like the desk panel: the visit is open, as on the locker map (13.2j).
              <div className="space-y-3 rounded-md border border-s-[3px] tone-edge p-3">
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
                    {/* Red and green like the «داخل باشگاه» board's buttons (13.2i). */}
                    <Button
                      size="sm"
                      variant="ghost"
                      className={cn(
                        stateEdge.destructive,
                        "hover:bg-destructive/10 hover:text-destructive",
                      )}
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
                      variant="ghost"
                      className={cn(
                        stateEdge.success,
                        "hover:bg-success/10 hover:text-success-ink",
                      )}
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
                <EmptyState icon={Footprints}>هنوز ورودی ثبت نشده است.</EmptyState>
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
