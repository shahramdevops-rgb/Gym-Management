import { Navigate, Outlet } from "react-router";

import { paths } from "@/app/paths";

import { isShopOnly, useRoles } from "../api";

/**
 * The gym's pages: every page of the panel but the shop's and the account's. A shop admin who
 * reaches one, after signing in or by an old link, is sent to the shop (ADR 0011); the API refuses
 * them anyway, so this only spares them a screen of refusals.
 *
 * The roles come from the access token until «me» answers (`useRoles`), so the desk's first screen
 * neither waits for «me» nor shows itself to a shop admin for the moment it takes.
 */
export function GymArea() {
  const roles = useRoles();

  if (isShopOnly(roles)) {
    return <Navigate to={paths.shopHome} replace />;
  }

  return <Outlet />;
}
