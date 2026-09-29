import { useAssignSubscription } from "../api";
import { PlanForm } from "./PlanForm";

interface AssignSubscriptionFormProps {
  memberId: string;
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Opens under the current subscription card: sell the member the plan the desk builds for them —
 * so many sessions, for the days they give (BUSINESS_RULES.md §3). It starts today or waits
 * behind what the member already holds (§4), and the member uses it the next time they come in.
 */
export function AssignSubscriptionForm({
  memberId,
  onDone,
  onCancel,
}: AssignSubscriptionFormProps) {
  const assign = useAssignSubscription();

  return (
    <PlanForm
      submitLabel="تأیید فروش"
      onSubmit={async (plan) => {
        await assign.mutateAsync({ memberId, ...plan });
        onDone();
      }}
      onCancel={onCancel}
    />
  );
}
