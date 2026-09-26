import { Link, useNavigate } from "react-router";

import { paths } from "@/app/paths";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { useCreatePlan, useSingleSessionPlan } from "../api";
import { PlanForm } from "../components/PlanForm";

/**
 * Owner only. A new plan starts active (docs/BUSINESS_RULES.md §3); the list opens after saving.
 *
 * This is also where the one single-session plan is created — the entry screen sends the Owner
 * here when it is missing. The choice is switched off once that plan exists; the API would refuse
 * a second one anyway, but a disabled box that says why beats a form that fails on save.
 */
export function CreatePlanPage() {
  const createPlan = useCreatePlan();
  const singleSessionPlan = useSingleSessionPlan();
  const navigate = useNavigate();
  // Checked by kind, not by "anything came back": the question is only about that one plan.
  const singleSessionTaken = singleSessionPlan.data?.kind === "SingleSession";

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>پلن جدید</CardTitle>
      </CardHeader>
      <CardContent>
        <PlanForm
          submitLabel="ثبت پلن"
          submittingLabel="در حال ثبت…"
          kindEditable
          singleSessionTaken={singleSessionTaken}
          onSubmit={async (input) => {
            await createPlan.mutateAsync(input);
            navigate(paths.plans);
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
