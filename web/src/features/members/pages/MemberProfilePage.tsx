import { Pencil } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";

import { paths } from "@/app/paths";
import { PageMessage } from "@/features/auth/components/PageMessage";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { formatDateTime } from "@/lib/format";

import { useMember, useSetMemberActive } from "../api";
import { MemberStatusBadge, PhoneNumber } from "../components/MembersTable";

/**
 * A member's basic details. Later phases add sections here: subscription, payments, check-in,
 * locker, cafe purchases.
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

  if (member.isPending) {
    return <PageMessage>در حال بارگذاری…</PageMessage>;
  }

  if (member.isError) {
    return <PageMessage>{errorMessage(member.error)}</PageMessage>;
  }

  const current = member.data;

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
    <div className="max-w-2xl space-y-4">
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
    </div>
  );
}
