import { Navigate, Outlet, useLocation } from "react-router";

import { paths } from "@/app/paths";

import { useSessionState } from "../session";
import { PageMessage } from "./PageMessage";

/**
 * The gate in front of every page except login.
 *
 * - While the session is being restored from the refresh cookie, it waits instead of flashing
 *   the login page at someone who is in fact logged in.
 * - Signed out: to the login page, remembering where the user was going.
 * - Must change password: to the change-password page, whatever they asked for. The API
 *   enforces the same rule (Auth.PasswordChangeRequired); this only avoids showing screens
 *   that would fail.
 */
export function RequireAuth() {
  const state = useSessionState();
  const location = useLocation();

  if (state.status === "restoring") {
    return <PageMessage>در حال بارگذاری…</PageMessage>;
  }

  if (state.status === "signedOut") {
    return <Navigate to={paths.login} replace state={{ from: location.pathname }} />;
  }

  if (state.session.mustChangePassword && location.pathname !== paths.changePassword) {
    return <Navigate to={paths.changePassword} replace />;
  }

  return <Outlet />;
}
