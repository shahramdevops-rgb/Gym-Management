import type { ReactNode } from "react";
import { createBrowserRouter, Navigate, type RouteObject } from "react-router";

import { RequireAuth } from "@/features/auth/components/RequireAuth";
import { RequireRole } from "@/features/auth/components/RequireRole";
import { ChangePasswordPage } from "@/features/auth/pages/ChangePasswordPage";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { CurrentlyInsidePage } from "@/features/attendance/pages/CurrentlyInsidePage";
import { CafeMenuPage } from "@/features/cafe/pages/CafeMenuPage";
import { CafeOrdersPage } from "@/features/cafe/pages/CafeOrdersPage";
import { CafeTillPage } from "@/features/cafe/pages/CafeTillPage";
import { ExpensesPage } from "@/features/expenses/pages/ExpensesPage";
import { HistoryPage } from "@/features/history/pages/HistoryPage";
import { LockersPage } from "@/features/lockers/pages/LockersPage";
import { CreateMemberPage } from "@/features/members/pages/CreateMemberPage";
import { EditMemberPage } from "@/features/members/pages/EditMemberPage";
import { MemberProfilePage } from "@/features/members/pages/MemberProfilePage";
import { MembersPage } from "@/features/members/pages/MembersPage";
import { PayablesPage } from "@/features/payables/pages/PayablesPage";
import { SettingsPage } from "@/features/settings/pages/SettingsPage";
import { SmsMessagesPage } from "@/features/sms/pages/SmsMessagesPage";
import { SmsSettingsPage } from "@/features/sms/pages/SmsSettingsPage";
import { StaffPage } from "@/features/staff/pages/StaffPage";
import { StatusPage } from "@/features/status/pages/StatusPage";

import { AppShell } from "./AppShell";
import { PageError } from "./PageError";
import { paths } from "./paths";

/** A route only the Owner may open. */
function ownerOnly(path: string, page: ReactNode): RouteObject {
  return { path, element: <RequireRole role="Owner">{page}</RequireRole> };
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
              { path: paths.members, element: <MembersPage /> },
              { path: paths.newMember, element: <CreateMemberPage /> },
              { path: paths.member(":id"), element: <MemberProfilePage /> },
              { path: paths.editMember(":id"), element: <EditMemberPage /> },
              { path: paths.attendance, element: <CurrentlyInsidePage /> },
              { path: paths.history, element: <HistoryPage /> },
              { path: paths.cafe, element: <CafeTillPage /> },
              { path: paths.cafeOrders, element: <CafeOrdersPage /> },
              { path: paths.cafeMenu, element: <CafeMenuPage /> },
              { path: paths.status, element: <StatusPage /> },
              { path: paths.changePassword, element: <ChangePasswordPage /> },
              {
                // Loaded the first time it is opened: it brings Recharts, which nobody at the
                // desk needs, so the rest of the app stays one smaller download.
                path: paths.dashboard,
                lazy: async () => {
                  const { DashboardPage } =
                    await import("@/features/dashboard/pages/DashboardPage");
                  return {
                    element: (
                      <RequireRole role="Owner">
                        <DashboardPage />
                      </RequireRole>
                    ),
                  };
                },
              },
              ownerOnly(paths.settings, <SettingsPage />),
              ownerOnly(paths.smsSettings, <SmsSettingsPage />),
              ownerOnly(paths.smsMessages, <SmsMessagesPage />),
              ownerOnly(paths.expenses, <ExpensesPage />),
              ownerOnly(paths.payables, <PayablesPage />),
              ownerOnly(paths.staff, <StaffPage />),
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
