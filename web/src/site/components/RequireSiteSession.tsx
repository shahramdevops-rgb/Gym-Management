import { Navigate, Outlet } from "react-router";

import { LoadingState } from "@/components/States";

import { useSiteSessionState } from "../auth/session";
import { sitePaths } from "../paths";
import { SiteFrame } from "./SiteFrame";

/** Anyone not signed in goes to the sign-in screen; a reload waits for the cookie first. */
export function RequireSiteSession() {
  const state = useSiteSessionState();

  if (state.status === "restoring") {
    return (
      <SiteFrame>
        <LoadingState />
      </SiteFrame>
    );
  }

  return state.status === "signedIn" ? <Outlet /> : <Navigate to={sitePaths.signIn} replace />;
}
