import { Link } from "react-router";

import { paths } from "@/app/paths";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { formatMoney, formatNumber } from "@/lib/format";

import type { Plan } from "../api";

interface PlansTableProps {
  plans: Plan[];
  /** Disables the activate/deactivate buttons while a change is being saved. */
  busy: boolean;
  onToggleActive: (plan: Plan) => void;
}

/** The Owner's plan list: what each plan gives, its price and status, and its actions. */
export function PlansTable({ plans, busy, onToggleActive }: PlansTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-muted-foreground">
            <th className="py-2 text-start font-medium">نام</th>
            <th className="py-2 text-start font-medium">مدت</th>
            <th className="py-2 text-start font-medium">جلسات</th>
            <th className="py-2 text-start font-medium">قیمت</th>
            <th className="py-2 text-start font-medium">وضعیت</th>
            <th className="py-2 text-start font-medium">
              <span className="sr-only">عملیات</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {plans.map((plan) => (
            <tr key={plan.id} className="border-b">
              <td className="py-2 font-medium">{plan.name}</td>
              <td className="py-2">{formatNumber(Number(plan.durationDays))} روز</td>
              <td className="py-2">
                {plan.sessionCount === null
                  ? "نامحدود"
                  : `${formatNumber(Number(plan.sessionCount))} جلسه`}
              </td>
              <td className="py-2">{formatMoney(Number(plan.price))}</td>
              <td className="py-2">
                <Badge variant={plan.isActive ? "success" : "secondary"}>
                  {plan.isActive ? "فعال" : "غیرفعال"}
                </Badge>
              </td>
              <td className="py-2">
                <div className="flex justify-end gap-2">
                  <Button asChild size="sm" variant="outline">
                    <Link to={paths.editPlan(plan.id)} aria-label={`ویرایش ${plan.name}`}>
                      ویرایش
                    </Link>
                  </Button>
                  <Button
                    size="sm"
                    variant={plan.isActive ? "destructive" : "secondary"}
                    disabled={busy}
                    onClick={() => onToggleActive(plan)}
                  >
                    {plan.isActive ? "غیرفعال‌سازی" : "فعال‌سازی"}
                  </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
