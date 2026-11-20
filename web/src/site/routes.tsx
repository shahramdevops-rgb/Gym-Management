import type { ComponentType } from "react";
import { Navigate, type RouteObject } from "react-router";

import { LoadingState } from "@/components/States";

import { SignInPage } from "./auth/pages/SignInPage";
import { RequireSiteSession } from "./components/RequireSiteSession";
import { SiteFrame } from "./components/SiteFrame";
import { HomePage } from "./home/HomePage";
import { sitePaths } from "./paths";

/**
 * A page downloaded only when it is first opened (roadmap 15.3, 15.7c). The first download, which
 * every opening of the app waits for, then holds only the sign-in screen and the home; the rest,
 * and what only they use (the date pickers' calendar), come when needed. Moving from page to page,
 * the router keeps the page that is on screen until the next one's file is in; opened straight on
 * its address (a reload, a link), the page shows `LoadingState` meanwhile. The installed app's
 * service worker keeps every file, so offline nothing is missing (site/sw.js).
 */
function lazyPage(
  load: () => Promise<ComponentType>,
): Pick<RouteObject, "hydrateFallbackElement" | "lazy"> {
  return {
    hydrateFallbackElement: (
      <SiteFrame>
        <LoadingState />
      </SiteFrame>
    ),
    lazy: async () => {
      const Page = await load();
      return { element: <Page /> };
    },
  };
}

/** The site app's routes; the tests render the same table (src/site/test/renderSite.tsx). */
export const siteRoutes: RouteObject[] = [
  { path: sitePaths.signIn, element: <SignInPage /> },
  // Only a new phone needs it: the birth date's dropdowns and §2's rules.
  {
    path: sitePaths.signUp,
    ...lazyPage(async () => (await import("./auth/pages/SignUpPage")).SignUpPage),
  },
  {
    element: <RequireSiteSession />,
    children: [
      { path: sitePaths.home, element: <HomePage /> },
      {
        path: sitePaths.trainers,
        ...lazyPage(async () => (await import("./coaching/TrainersPage")).TrainersPage),
      },
      {
        path: sitePaths.timetable,
        ...lazyPage(async () => (await import("./timetable/TimetablePage")).TimetablePage),
      },
      {
        path: sitePaths.students,
        ...lazyPage(async () => (await import("./coaching/StudentsPage")).StudentsPage),
      },
      {
        path: sitePaths.inactiveStudents,
        ...lazyPage(
          async () => (await import("./coaching/InactiveStudentsPage")).InactiveStudentsPage,
        ),
      },
      {
        path: sitePaths.studentProfilePattern,
        ...lazyPage(async () => (await import("./coaching/StudentProfilePage")).StudentProfilePage),
      },
      {
        path: sitePaths.studentPattern,
        ...lazyPage(
          async () => (await import("./bookings/StudentSchedulePage")).StudentSchedulePage,
        ),
      },
      {
        path: sitePaths.mySessions,
        ...lazyPage(async () => (await import("./bookings/MySessionsPage")).MySessionsPage),
      },
      {
        path: sitePaths.myAttendance,
        ...lazyPage(async () => (await import("./attendance/MyAttendancePage")).MyAttendancePage),
      },
      {
        path: sitePaths.today,
        ...lazyPage(async () => (await import("./attendance/DayPage")).DayPage),
      },
      {
        path: sitePaths.studentAttendancePattern,
        ...lazyPage(
          async () => (await import("./attendance/StudentAttendancePage")).StudentAttendancePage,
        ),
      },
    ],
  },
  { path: "*", element: <Navigate to={sitePaths.home} replace /> },
];
