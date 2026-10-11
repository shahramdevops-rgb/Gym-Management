import { Fragment, useState } from "react";

import { PageHeader } from "@/components/PageHeader";
import { EmptyState, LoadingState } from "@/components/States";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { errorMessage } from "@/lib/errors";

import { useSetStaffActive, useStaffList, useUnlockStaff, type StaffMember } from "../api";
import { CreateStaffForm } from "../components/CreateStaffForm";
import { ResetPasswordForm } from "../components/ResetPasswordForm";

/**
 * Owner only (the route is wrapped in RequireRole): create, list, deactivate, reset password, and
 * unlock an account someone else's wrong guesses locked.
 */
export function StaffPage() {
  const [page, setPage] = useState(1);
  const staff = useStaffList(page);
  const setActive = useSetStaffActive();
  const unlock = useUnlockStaff();
  const [resettingId, setResettingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  async function unlockAccount(member: StaffMember) {
    setNotice(null);
    try {
      await unlock.mutateAsync(member.id);
      setNotice({
        kind: "success",
        text: `قفل حساب «${member.fullName}» باز شد. او با همان رمز قبلی وارد می‌شود.`,
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  async function toggleActive(member: StaffMember) {
    setNotice(null);
    try {
      await setActive.mutateAsync({ id: member.id, active: !member.isActive });
      setNotice({
        kind: "success",
        text: member.isActive
          ? `حساب «${member.fullName}» غیرفعال شد و از همهٔ دستگاه‌ها خارج شد.`
          : `حساب «${member.fullName}» دوباره فعال شد.`,
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title="کارمندان" description="حساب‌های کاربری کارمندان پذیرش." />

      <CreateStaffForm />

      <Card>
        <CardHeader>
          <CardTitle>فهرست کارمندان</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {notice !== null && (
            <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
              {notice.text}
            </Alert>
          )}

          {staff.isPending && <LoadingState />}
          {staff.isError && <Alert variant="destructive">{errorMessage(staff.error)}</Alert>}

          {staff.isSuccess && staff.data.items.length === 0 && (
            <EmptyState>هنوز هیچ کارمندی ثبت نشده است.</EmptyState>
          )}

          {staff.isSuccess && staff.data.items.length > 0 && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>نام</TableHead>
                  <TableHead>نام کاربری</TableHead>
                  <TableHead>وضعیت</TableHead>
                  <TableHead>
                    <span className="sr-only">عملیات</span>
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {staff.data.items.map((member) => (
                  <Fragment key={member.id}>
                    <TableRow>
                      <TableCell>{member.fullName}</TableCell>
                      <TableCell dir="ltr">
                        <span className="block text-end">{member.userName}</span>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          <Badge variant={member.isActive ? "success" : "secondary"}>
                            {member.isActive ? "فعال" : "غیرفعال"}
                          </Badge>
                          {member.mustChangePassword && <Badge variant="secondary">رمز موقت</Badge>}
                          {member.isLockedOut && <Badge variant="destructive">قفل‌شده</Badge>}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex justify-end gap-2">
                          {member.isLockedOut && (
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={unlock.isPending}
                              onClick={() => void unlockAccount(member)}
                            >
                              باز کردن قفل
                            </Button>
                          )}
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() =>
                              setResettingId(resettingId === member.id ? null : member.id)
                            }
                          >
                            بازنشانی رمز
                          </Button>
                          <Button
                            size="sm"
                            variant={member.isActive ? "destructive" : "secondary"}
                            disabled={setActive.isPending}
                            onClick={() => void toggleActive(member)}
                          >
                            {member.isActive ? "غیرفعال‌سازی" : "فعال‌سازی"}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                    {resettingId === member.id && (
                      <TableRow className="bg-muted/40">
                        <TableCell colSpan={4} className="p-3">
                          <ResetPasswordForm
                            staff={member}
                            onCancel={() => setResettingId(null)}
                            onDone={(text) => {
                              setResettingId(null);
                              setNotice({ kind: "success", text });
                            }}
                          />
                        </TableCell>
                      </TableRow>
                    )}
                  </Fragment>
                ))}
              </TableBody>
            </Table>
          )}

          {staff.isSuccess && (
            <Pager page={page} pageCount={staff.data.pageCount} onPageChange={setPage} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
