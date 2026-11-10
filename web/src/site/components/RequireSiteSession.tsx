import { useEffect } from "react";
import { Navigate, Outlet, useLocation } from "react-router";

import { LoadingState } from "@/components/States";

import { returnToStore } from "../auth/returnTo";
import { useSiteSessionState } from "../auth/session";
import { sitePaths } from "../paths";
import { SiteFrame } from "./SiteFrame";
import { UnreachableNotice } from "./UnreachableNotice";

/**
 * Anyone not signed in goes to the sign-in screen; a reload waits for the cookie first, and a
 * server that cannot be reached is tried again rather than taken for a session that ended. The page
 * they were opening is remembered and opened once they are signed in (roadmap 15.4d).
 */
export function RequireSiteSession() {
  const state = useSiteSessionState();
  const location = useLocation();
  const signedIn = state.status === "signedIn";

  useEffect(() => {
    if (signedIn) {
      returnToStore.clear();
    }
  }, [signedIn]);

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

  if (!signedIn) {
    returnToStore.remember(location.pathname + location.search);
    return <Navigate to={sitePaths.signIn} replace />;
  }

  return <Outlet />;
}
