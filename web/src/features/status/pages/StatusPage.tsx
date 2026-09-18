import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle>وضعیت سرور</CardTitle>
        <CardDescription>ارتباط با سرور و پایگاه داده</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2">
        {health.isPending && <p className="text-muted-foreground">در حال بررسی…</p>}

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
  );
}
