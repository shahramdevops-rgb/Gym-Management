import { useForm } from "react-hook-form";

import { SelectField } from "@/components/FormField";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { usePlanList } from "@/features/plans/api";
import { applyServerErrors, zodResolver } from "@/lib/forms";
import { formatMoney } from "@/lib/format";

import { useAssignSubscription } from "../api";
import { assignSubscriptionSchema, emptyAssignSubscriptionValues } from "../schemas";

const codeFields = {
  "Subscriptions.PlanRequired": "planId",
} as const;

interface AssignSubscriptionFormProps {
  memberId: string;
  onDone: () => void;
  onCancel: () => void;
}

/** Opens under the current subscription card: sell the member a chosen plan (BUSINESS_RULES.md §4). */
export function AssignSubscriptionForm({
  memberId,
  onDone,
  onCancel,
}: AssignSubscriptionFormProps) {
  const plans = usePlanList({ isActive: true, page: 1 });
  const assign = useAssignSubscription();

  const form = useForm({
    resolver: zodResolver(assignSubscriptionSchema),
    defaultValues: emptyAssignSubscriptionValues,
  });

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await assign.mutateAsync({ memberId, planId: values.planId });
      onDone();
    } catch (problem) {
      applyServerErrors(problem, form.setError, codeFields);
    }
  });

  const { errors, isSubmitting } = form.formState;

  return (
    <form className="flex flex-wrap items-end gap-3" onSubmit={onSubmit} noValidate>
      {errors.root?.server !== undefined && (
        <Alert variant="destructive">{errors.root.server.message}</Alert>
      )}

      <div className="min-w-64 flex-1">
        <SelectField label="پلن" error={errors.planId?.message} {...form.register("planId")}>
          <option value="" disabled>
            انتخاب کنید
          </option>
          {plans.data?.items.map((plan) => (
            <option key={plan.id} value={plan.id}>
              {plan.name} — {formatMoney(plan.price)}
            </option>
          ))}
        </SelectField>
      </div>
      <Button type="submit" size="sm" disabled={isSubmitting || plans.isPending}>
        تأیید فروش
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
        انصراف
      </Button>
    </form>
  );
}
