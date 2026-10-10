import type { ComponentType } from "react";
import { createBrowserRouter, Navigate, type RouteObject } from "react-router";

import { PageMessage } from "@/features/auth/components/PageMessage";
import { RequireAuth } from "@/features/auth/components/RequireAuth";
import { RequireRole } from "@/features/auth/components/RequireRole";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { LockersPage } from "@/features/lockers/pages/LockersPage";

import { AppShell } from "./AppShell";
import { PageError } from "./PageError";
import { paths } from "./paths";

/**
 * A page downloaded the first time it is opened, not with the app (task 11.3). Only the login
 * page and the locker board, the desk's first screen, come with the app itself; every other
 * page is its own file, so the first load carries what the desk needs and nothing the Owner's
 * reports do. Each page is fetched once and then cached by the browser like the rest.
 */
function lazyPage(
  path: string,
  load: () => Promise<ComponentType>,
  ownerOnly = false,
): RouteObject {
  return {
    path,
    lazy: async () => {
      const Page = await load();
      return {
        element: ownerOnly ? (
          <RequireRole role="Owner">
            <Page />
          </RequireRole>
        ) : (
          <Page />
        ),
      };
    },
  };
}

/**
 * Route table. Login stands alone; everything else is behind RequireAuth, and pages for one
 * role are also wrapped in RequireRole. The API enforces the same rules, so these guards are
 * about not showing screens that would fail, not about security.
 */
export const routes: RouteObject[] = [
  { path: paths.login, element: <LoginPage /> },
  {
    element: <RequireAuth />,
    // Shown while the page asked for is still downloading, when the app opens straight on it
    // (a reload on /members). Without it the window would stay blank until the file arrived.
    hydrateFallbackElement: <PageMessage>در حال بارگذاری…</PageMessage>,
    children: [
      {
        element: <AppShell />,
        children: [
          {
            // A page that fails while rendering shows PageError inside the frame, instead of
            // React Router's own screen replacing the whole app.
            errorElement: <PageError />,
            children: [
              { path: paths.home, element: <LockersPage /> },
              lazyPage(paths.members, () =>
                import("@/features/members/pages/MembersPage").then((m) => m.MembersPage),
              ),
              lazyPage(paths.newMember, () =>
                import("@/features/members/pages/CreateMemberPage").then((m) => m.CreateMemberPage),
              ),
              lazyPage(paths.member(":id"), () =>
                import("@/features/members/pages/MemberProfilePage").then(
                  (m) => m.MemberProfilePage,
                ),
              ),
              lazyPage(paths.editMember(":id"), () =>
                import("@/features/members/pages/EditMemberPage").then((m) => m.EditMemberPage),
              ),
              lazyPage(paths.attendance, () =>
                import("@/features/attendance/pages/CurrentlyInsidePage").then(
                  (m) => m.CurrentlyInsidePage,
                ),
              ),
              lazyPage(paths.history, () =>
                import("@/features/history/pages/HistoryPage").then((m) => m.HistoryPage),
              ),
              lazyPage(paths.cafe, () =>
                import("@/features/cafe/pages/CafeTillPage").then((m) => m.CafeTillPage),
              ),
              lazyPage(paths.cafeOrders, () =>
                import("@/features/cafe/pages/CafeOrdersPage").then((m) => m.CafeOrdersPage),
              ),
              lazyPage(paths.cafeMenu, () =>
                import("@/features/cafe/pages/CafeMenuPage").then((m) => m.CafeMenuPage),
              ),
              lazyPage(paths.status, () =>
                import("@/features/status/pages/StatusPage").then((m) => m.StatusPage),
              ),
              lazyPage(paths.changePassword, () =>
                import("@/features/auth/pages/ChangePasswordPage").then(
                  (m) => m.ChangePasswordPage,
                ),
              ),
              lazyPage(
                paths.dashboard,
                () =>
                  import("@/features/dashboard/pages/DashboardPage").then((m) => m.DashboardPage),
                true,
              ),
              lazyPage(
                paths.settings,
                () => import("@/features/settings/pages/SettingsPage").then((m) => m.SettingsPage),
                true,
              ),
              lazyPage(
                paths.smsSettings,
                () => import("@/features/sms/pages/SmsSettingsPage").then((m) => m.SmsSettingsPage),
                true,
              ),
              lazyPage(
                paths.smsMessages,
                () => import("@/features/sms/pages/SmsMessagesPage").then((m) => m.SmsMessagesPage),
                true,
              ),
              lazyPage(
                paths.auditLogs,
                () => import("@/features/audit/pages/AuditLogsPage").then((m) => m.AuditLogsPage),
                true,
              ),
              lazyPage(
                paths.expenses,
                () => import("@/features/expenses/pages/ExpensesPage").then((m) => m.ExpensesPage),
                true,
              ),
              lazyPage(
                paths.payables,
                () => import("@/features/payables/pages/PayablesPage").then((m) => m.PayablesPage),
                true,
              ),
              lazyPage(
                paths.staff,
                () => import("@/features/staff/pages/StaffPage").then((m) => m.StaffPage),
                true,
              ),
            ],
          },
        ],
      },
    ],
  },
  // The member search screen was folded into the member list; an old bookmark lands there.
  { path: "/search", element: <Navigate to={paths.members} replace /> },
  { path: "*", element: <Navigate to={paths.home} replace /> },
];

export const router = createBrowserRouter(routes);
