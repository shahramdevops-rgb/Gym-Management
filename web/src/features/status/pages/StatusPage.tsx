import { PageHeader } from "@/components/PageHeader";
import { LoadingState } from "@/components/States";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { formatDateTime } from "@/lib/format";

import { useHealth, type HealthStatus } from "../api";

const statusLabels: Record<HealthStatus, string> = {
  Healthy: "سالم",
  Degraded: "کارکرد محدود",
  Unhealthy: "ناسالم",
};

const statusVariants: Record<HealthStatus, "success" | "secondary" | "destructive"> = {
  Healthy: "success",
  Degraded: "secondary",
  Unhealthy: "destructive",
};

export function StatusPage() {
  const health = useHealth();

  return (
    <div className="max-w-md space-y-6">
      <PageHeader title="وضعیت سرور" description="ارتباط با سرور و پایگاه داده" />
      <Card>
        <CardContent className="space-y-2">
          {health.isPending && <LoadingState className="py-0">در حال بررسی…</LoadingState>}

          {health.isError && <Badge variant="destructive">سرور در دسترس نیست</Badge>}

          {health.isSuccess && (
            <>
              <Badge variant={statusVariants[health.data.status]}>
                {statusLabels[health.data.status]}
              </Badge>
              <p className="text-sm text-muted-foreground">
                آخرین بررسی: {formatDateTime(health.data.checkedAt)}
              </p>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
