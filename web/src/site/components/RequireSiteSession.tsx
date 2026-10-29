import { Navigate, Outlet } from "react-router";

import { LoadingState } from "@/components/States";

import { useSiteSessionState } from "../auth/session";
import { sitePaths } from "../paths";
import { SiteFrame } from "./SiteFrame";
import { UnreachableNotice } from "./UnreachableNotice";

/**
 * Anyone not signed in goes to the sign-in screen; a reload waits for the cookie first, and a
 * server that cannot be reached is tried again rather than taken for a session that ended.
 */
export function RequireSiteSession() {
  const state = useSiteSessionState();

  if (state.status === "restoring") {
    return (
      <SiteFrame>
        <LoadingState />
      </SiteFrame>
    );
  }

  if (state.status === "unreachable") {
    return <UnreachableNotice />;
  }

  return state.status === "signedIn" ? <Outlet /> : <Navigate to={sitePaths.signIn} replace />;
}
