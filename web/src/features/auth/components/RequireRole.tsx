import { Lock } from "lucide-react";
import type { ReactNode } from "react";
import { Navigate } from "react-router";

import { useCurrentUser, useRoles, type Role } from "../api";
import { PageLoading, PageMessage } from "./PageMessage";

/**
 * Shows the page only to users with the role. The API refuses the requests anyway
 * (Policies.OwnerOnly); this keeps a Staff member who types /staff into the address bar from
 * seeing a screen of failed requests.
 *
 * `otherwise` sends everyone else there instead of telling them they may not: for a page reached
 * from a phone's long-press shortcut, which every installed panel lists, Staff included (the
 * manifest is one file for everyone). A Staff member who presses «خلاصهٔ امروز» lands on the
 * locker map rather than on a refusal (roadmap 13.5).
 *
 * `role` may name several roles, any of which opens the page: the shop's pages are the shop admin's
 * and the Owner's (BUSINESS_RULES.md §16).
 */
export function RequireRole({
  role,
  otherwise,
  children,
}: {
  role: Role | readonly Role[];
  otherwise?: string;
  children: ReactNode;
}) {
  const user = useCurrentUser();
  // From the token until «me» answers (`useRoles`); a token that says nothing waits for «me».
  const roles = useRoles();
  const wanted: readonly Role[] = typeof role === "string" ? [role] : role;

  if (roles.length === 0 && user.isPending) {
    return <PageLoading />;
  }

  if (!roles.some((held) => wanted.includes(held))) {
    // Only when the role is known: had the request for it failed (a phone's connection still
    // waking up), the Owner would land on the locker map with no word and no way Back.
    if (otherwise !== undefined && roles.length > 0) {
      return <Navigate to={otherwise} replace />;
    }

    return <PageMessage icon={Lock}>اجازهٔ دسترسی به این بخش را ندارید.</PageMessage>;
  }

  return children;
}
