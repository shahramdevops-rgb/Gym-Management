import { Lock } from "lucide-react";
import type { ReactNode } from "react";

import { hasRole, useCurrentUser, type Role } from "../api";
import { PageLoading, PageMessage } from "./PageMessage";

/**
 * Shows the page only to users with the role. The API refuses the requests anyway
 * (Policies.OwnerOnly); this keeps a Staff member who types /staff into the address bar from
 * seeing a screen of failed requests.
 */
export function RequireRole({ role, children }: { role: Role; children: ReactNode }) {
  const user = useCurrentUser();

  if (user.isPending) {
    return <PageLoading />;
  }

  if (!hasRole(user.data, role)) {
    return <PageMessage icon={Lock}>اجازهٔ دسترسی به این بخش را ندارید.</PageMessage>;
  }

  return children;
}
