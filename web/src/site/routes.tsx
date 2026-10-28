import { Navigate, type RouteObject } from "react-router";

import { LoadingState } from "@/components/States";

import { SignInPage } from "./auth/pages/SignInPage";
import { RequireSiteSession } from "./components/RequireSiteSession";
import { SiteFrame } from "./components/SiteFrame";
import { HomePage } from "./home/HomePage";
import { sitePaths } from "./paths";

/** The site app's routes; the tests render the same table (src/site/test/renderSite.tsx). */
export const siteRoutes: RouteObject[] = [
  { path: sitePaths.signIn, element: <SignInPage /> },
  // Downloaded only by a new phone: the birth date's dropdowns and §2's rules stay out of the
  // first download, which every sign-in waits for.
  {
    path: sitePaths.signUp,
    // Shown while the file downloads when the app opens straight on this address (a reload).
    hydrateFallbackElement: (
      <SiteFrame>
        <LoadingState />
      </SiteFrame>
    ),
    lazy: async () => {
      const { SignUpPage } = await import("./auth/pages/SignUpPage");
      return { element: <SignUpPage /> };
    },
  },
  {
    element: <RequireSiteSession />,
    children: [{ path: sitePaths.home, element: <HomePage /> }],
  },
  { path: "*", element: <Navigate to={sitePaths.home} replace /> },
];
