import { Link, useNavigate } from "react-router";

import { paths } from "@/app/paths";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { useCreatePlan } from "../api";
import { PlanForm } from "../components/PlanForm";

/** Owner only. A new plan starts active (docs/BUSINESS_RULES.md §3); the list opens after saving. */
export function CreatePlanPage() {
  const createPlan = useCreatePlan();
  const navigate = useNavigate();

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle>پلن جدید</CardTitle>
      </CardHeader>
      <CardContent>
        <PlanForm
          submitLabel="ثبت پلن"
          submittingLabel="در حال ثبت…"
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
