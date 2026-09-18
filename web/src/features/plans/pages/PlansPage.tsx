import { Plus } from "lucide-react";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";

import { paths } from "@/app/paths";
import { Pager } from "@/components/Pager";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";
import { toPersianDigits } from "@/lib/format";
import { pageFromParams } from "@/lib/searchParams";

import { usePlanList, useSetPlanActive, type Plan } from "../api";
import { PlansTable } from "../components/PlansTable";

type StatusFilter = "all" | "active" | "inactive";

const filters: { value: StatusFilter; label: string }[] = [
  { value: "all", label: "همه" },
  { value: "active", label: "فعال" },
  { value: "inactive", label: "غیرفعال" },
];

function statusFromParams(params: URLSearchParams): StatusFilter {
  const status = params.get("status");

  return status === "active" || status === "inactive" ? status : "all";
}

/**
 * Owner only (the route is wrapped in RequireRole): every plan, active ones first
 * (docs/BUSINESS_RULES.md §3), with create, edit, activate and deactivate. The filter and the
 * page are in the URL (`/plans?status=inactive&page=2`), like the member list.
 */
export function PlansPage() {
  const [params, setParams] = useSearchParams();
  const status = statusFromParams(params);
  const page = pageFromParams(params);
  const plans = usePlanList({
    isActive: status === "all" ? undefined : status === "active",
    page,
  });
  const setActive = useSetPlanActive();
  const [notice, setNotice] = useState<{ kind: "success" | "destructive"; text: string } | null>(
    null,
  );

  function show(nextStatus: StatusFilter, nextPage = 1) {
    const next: Record<string, string> = {};
    if (nextStatus !== "all") {
      next.status = nextStatus;
    }
    if (nextPage > 1) {
      next.page = String(nextPage);
    }
    setParams(next);
  }

  async function toggleActive(plan: Plan) {
    setNotice(null);
    try {
      await setActive.mutateAsync({ id: plan.id, active: !plan.isActive });
      setNotice({
        kind: "success",
        // BUSINESS_RULES.md §3: deactivating stops new sales only.
        text: plan.isActive
          ? `پلن «${plan.name}» غیرفعال شد و دیگر فروخته نمی‌شود. اشتراک‌های فعلی تغییری نمی‌کنند.`
          : `پلن «${plan.name}» دوباره فعال شد.`,
      });
    } catch (problem) {
      setNotice({ kind: "destructive", text: errorMessage(problem) });
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-xl font-bold">پلن‌ها</h2>
        <Button asChild>
          <Link to={paths.newPlan}>
            <Plus aria-hidden />
            پلن جدید
          </Link>
        </Button>
      </div>

      <Card>
        <CardHeader className="flex flex-wrap items-center justify-between gap-3">
          <CardTitle>
            فهرست پلن‌ها
            {plans.isSuccess && (
              <span className="ms-2 text-sm font-normal text-muted-foreground">
                ({toPersianDigits(plans.data.totalCount)})
              </span>
            )}
          </CardTitle>
          <div role="group" aria-label="وضعیت پلن" className="flex gap-1">
            {filters.map((filter) => (
              <Button
                key={filter.value}
                size="sm"
                variant={status === filter.value ? "secondary" : "ghost"}
                aria-pressed={status === filter.value}
                onClick={() => show(filter.value)}
              >
                {filter.label}
              </Button>
            ))}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {notice !== null && (
            <Alert variant={notice.kind} role={notice.kind === "success" ? "status" : "alert"}>
              {notice.text}
            </Alert>
          )}

          {plans.isPending && <p className="text-muted-foreground">در حال بارگذاری…</p>}
          {plans.isError && <Alert variant="destructive">{errorMessage(plans.error)}</Alert>}

          {plans.isSuccess && plans.data.items.length === 0 && (
            <p className="text-muted-foreground">
              {status === "all" ? "هنوز هیچ پلنی تعریف نشده است." : "پلنی با این وضعیت نیست."}
            </p>
          )}

          {plans.isSuccess && plans.data.items.length > 0 && (
            <>
              <PlansTable
                plans={plans.data.items}
                busy={setActive.isPending}
                onToggleActive={(plan) => void toggleActive(plan)}
              />
              <Pager
                page={page}
                pageCount={plans.data.pageCount}
                onPageChange={(next) => show(status, next)}
              />
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
