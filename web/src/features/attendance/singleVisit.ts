import { useMutation, useQueryClient } from "@tanstack/react-query";

import { lockerKeys } from "@/features/lockers/api";
import { memberKeys } from "@/features/members/api";
import { subscriptionKeys, type Subscription } from "@/features/subscriptions/api";
import { api } from "@/lib/api/client";

import { attendanceKeys, type Attendance } from "./api";

/**
 * Codes that mean "this member cannot come in today because of their subscription", as opposed to
 * anything else check-in can refuse (an inactive member, someone already inside, a race).
 *
 * This list is what turns a refusal into the offer of a single visit. It is deliberately a list of
 * reasons rather than a guess made before the call: the API decides whether someone can come in,
 * and asking it is the only way to be sure. Deriving the same answer in the browser would mean a
 * second copy of the rule in BUSINESS_RULES.md §4 and §7, and the copy would be the one that was
 * wrong (the same reasoning that keeps locker occupancy derived, task 6.5.1).
 */
const needsASubscription = new Set([
  "Attendance.NoSubscription",
  "Subscriptions.Expired",
  "Subscriptions.NoSessionsLeft",
  "Subscriptions.Frozen",
  "Subscriptions.NotStarted",
  "Subscriptions.NextStartsTomorrow",
  "Subscriptions.Cancelled",
]);

/** Whether a failed check-in failed for want of a usable subscription. */
export function isMissingSubscription(problem: unknown): boolean {
  const code =
    typeof problem === "object" && problem !== null && "code" in problem
      ? (problem as { code?: unknown }).code
      : undefined;

  return typeof code === "string" && needsASubscription.has(code);
}

export interface SingleVisitResult {
  subscription: Subscription;
  attendance: Attendance;
}

/**
 * Sells one visit and checks the member in, in that order (BUSINESS_RULES.md §4
 * <i>Single-session subscriptions</i>, roadmap 6.5.4).
 *
 * Two requests, not one: a single visit is an ordinary subscription sold from the single-session
 * plan, and check-in is the same check-in as any other. There is no combined endpoint, and adding
 * one would put a second way to sell a subscription next to the first.
 *
 * They are not atomic, and that is survivable in a way the reverse order would not be: if the sale
 * succeeds and the check-in fails (someone took the locker in between, say), the member has a paid
 * visit for today and the desk checks them in from the map again. The failure is visible and
 * recoverable. A check-in that somehow preceded its sale would be neither.
 *
 * The visit is checked in with the place the desk clicked on the map (roadmap 6.5.5): `lockerId`,
 * or `null` for a reserve place.
 */
export function useSellSingleVisit() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      memberId,
      planId,
      lockerId,
    }: {
      memberId: string;
      planId: string;
      lockerId: string | null;
    }): Promise<SingleVisitResult> => {
      const sale = await api.POST("/api/members/{memberId}/subscriptions", {
        params: { path: { memberId } },
        body: { planId },
      });
      if (sale.error !== undefined) {
        throw sale.error;
      }

      const visit = await api.POST("/api/members/{memberId}/attendance/check-in", {
        params: { path: { memberId } },
        body: { lockerId },
      });
      if (visit.error !== undefined) {
        throw visit.error;
      }

      return { subscription: sale.data, attendance: visit.data };
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: attendanceKeys.all }),
        queryClient.invalidateQueries({ queryKey: lockerKeys.all }),
        queryClient.invalidateQueries({ queryKey: subscriptionKeys.all }),
        queryClient.invalidateQueries({ queryKey: memberKeys.all }),
      ]),
  });
}
