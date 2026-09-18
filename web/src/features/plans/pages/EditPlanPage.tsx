import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";

import { paths } from "@/app/paths";
import { PageMessage } from "@/features/auth/components/PageMessage";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { errorMessage } from "@/lib/errors";

import { usePlan, useUpdatePlan, type Plan } from "../api";
import { PlanForm } from "../components/PlanForm";
import type { PlanValues } from "../schemas";

const changedConcurrently = "Plans.ChangedConcurrently";

function isCode(problem: unknown, code: string): boolean {
  return (
    typeof problem === "object" && problem !== null && "code" in problem && problem.code === code
  );
}

/** The form's text values for a plan as the API sent it. */
function formValues(plan: Plan): PlanValues {
  return {
    name: plan.name,
    durationDays: String(plan.durationDays),
    unlimitedSessions: plan.sessionCount === null,
    sessionCount: plan.sessionCount === null ? "" : String(plan.sessionCount),
    price: String(plan.price),
  };
}

/**
 * Owner only. Edits a plan, active or not. Existing subscriptions keep the values they were
 * sold with (docs/BUSINESS_RULES.md §3), so this changes future sales only.
 *
 * Like the member edit page, the form is filled from one `version` and sends it back; if
 * someone saved meanwhile, the page says so and offers to load their version.
 */
export function EditPlanPage() {
  const { id = "" } = useParams();
  const plan = usePlan(id);
  const updatePlan = useUpdatePlan();
  const navigate = useNavigate();
  const [stale, setStale] = useState(false);
  const [reloading, setReloading] = useState(false);

  if (plan.isPending) {
    return <PageMessage>در حال بارگذاری…</PageMessage>;
  }

  if (plan.isError) {
    return <PageMessage>{errorMessage(plan.error)}</PageMessage>;
  }

  const current = plan.data;

  async function reload() {
    setReloading(true);
    await plan.refetch();
    setReloading(false);
    setStale(false);
  }

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>ویرایش پلن «{current.name}»</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          تغییرات فقط روی فروش‌های بعدی اثر دارد؛ اشتراک‌هایی که قبلاً فروخته شده‌اند تغییری
          نمی‌کنند.
        </p>

        {stale && (
          <Alert
            variant="destructive"
            className="flex flex-wrap items-center justify-between gap-3"
          >
            <span>{errorMessage({ code: changedConcurrently })}</span>
            <Button size="sm" variant="outline" disabled={reloading} onClick={() => void reload()}>
              بارگذاری اطلاعات تازه
            </Button>
          </Alert>
        )}

        {/* Keyed by version, so fresh data rebuilds the form with its values. */}
        <PlanForm
          key={String(current.version)}
          defaultValues={formValues(current)}
          submitLabel="ذخیره"
          submittingLabel="در حال ذخیره…"
          onSubmit={async (input) => {
            try {
              await updatePlan.mutateAsync({ id, version: current.version, ...input });
              navigate(paths.plans);
            } catch (problem) {
              if (!isCode(problem, changedConcurrently)) {
                throw problem;
              }
              setStale(true);
            }
          }}
          actions={
            <Button asChild variant="ghost">
              <Link to={paths.plans}>انصراف</Link>
            </Button>
          }
        />
      </CardContent>
    </Card>
  );
}
