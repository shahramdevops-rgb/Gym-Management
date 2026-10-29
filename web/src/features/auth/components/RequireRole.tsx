import { Lock } from "lucide-react";
import type { ReactNode } from "react";
import { Navigate } from "react-router";

import { hasRole, useCurrentUser, type Role } from "../api";
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
 */
export function RequireRole({
  role,
  otherwise,
  children,
}: {
  role: Role;
  otherwise?: string;
  children: ReactNode;
}) {
  const user = useCurrentUser();

  if (user.isPending) {
    return <PageLoading />;
  }

  if (!hasRole(user.data, role)) {
    // Only when the role is known: had the request for it failed (a phone's connection still
    // waking up), the Owner would land on the locker map with no word and no way Back.
    if (otherwise !== undefined && user.isSuccess) {
      return <Navigate to={otherwise} replace />;
    }

    return <PageMessage icon={Lock}>اجازهٔ دسترسی به این بخش را ندارید.</PageMessage>;
  }

  return children;
}
