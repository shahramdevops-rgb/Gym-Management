import { Fragment, useState } from "react";

import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { useSetStaffActive, useStaffList, type StaffMember } from "../api";
import { CreateStaffForm } from "../components/CreateStaffForm";
import { ResetPasswordForm } from "../components/ResetPasswordForm";

/** Owner only (the route is wrapped in RequireRole): create, list, deactivate, reset password. */
export function StaffPage() {
  const [page, setPage] = useState(1);
  const staff = useStaffList(page);
  const setActive = useSetStaffActive();
  const [resettingId, setResettingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

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
      <h2 className="text-xl font-bold">کارمندان</h2>

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

          {staff.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {staff.isError && <Alert variant="destructive">{errorMessage(staff.error)}</Alert>}

          {staff.isSuccess && staff.data.items.length === 0 && (
            <p className="text-muted-foreground">هنوز هیچ کارمندی ثبت نشده است.</p>
          )}

          {staff.isSuccess && staff.data.items.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-muted-foreground">
                    <th className="py-2 text-start font-medium">نام</th>
                    <th className="py-2 text-start font-medium">نام کاربری</th>
                    <th className="py-2 text-start font-medium">وضعیت</th>
                    <th className="py-2 text-start font-medium">
                      <span className="sr-only">عملیات</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {staff.data.items.map((member) => (
                    <Fragment key={member.id}>
                      <tr className="border-b">
                        <td className="py-2">{member.fullName}</td>
                        <td className="py-2" dir="ltr">
                          <span className="block text-end">{member.userName}</span>
                        </td>
                        <td className="py-2">
                          <div className="flex flex-wrap gap-1">
                            <Badge variant={member.isActive ? "success" : "secondary"}>
                              {member.isActive ? "فعال" : "غیرفعال"}
                            </Badge>
                            {member.mustChangePassword && (
                              <Badge variant="secondary">رمز موقت</Badge>
                            )}
                            {member.isLockedOut && <Badge variant="destructive">قفل‌شده</Badge>}
                          </div>
                        </td>
                        <td className="py-2">
                          <div className="flex justify-end gap-2">
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
                        </td>
                      </tr>
                      {resettingId === member.id && (
                        <tr className="border-b bg-muted/40">
                          <td colSpan={4} className="p-3">
                            <ResetPasswordForm
                              staff={member}
                              onCancel={() => setResettingId(null)}
                              onDone={(text) => {
                                setResettingId(null);
                                setNotice({ kind: "success", text });
                              }}
                            />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {staff.isSuccess && (
            <Pager page={page} pageCount={staff.data.pageCount} onPageChange={setPage} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
