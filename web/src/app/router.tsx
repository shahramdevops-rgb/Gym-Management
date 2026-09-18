import { createBrowserRouter, Navigate, type RouteObject } from "react-router";

import { RequireAuth } from "@/features/auth/components/RequireAuth";
import { RequireRole } from "@/features/auth/components/RequireRole";
import { ChangePasswordPage } from "@/features/auth/pages/ChangePasswordPage";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { CreateMemberPage } from "@/features/members/pages/CreateMemberPage";
import { EditMemberPage } from "@/features/members/pages/EditMemberPage";
import { HomePage } from "@/features/members/pages/HomePage";
import { MemberProfilePage } from "@/features/members/pages/MemberProfilePage";
import { MembersPage } from "@/features/members/pages/MembersPage";
import { StaffPage } from "@/features/staff/pages/StaffPage";
import { StatusPage } from "@/features/status/pages/StatusPage";

import { AppShell } from "./AppShell";
import { paths } from "./paths";

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
          { path: paths.home, element: <HomePage /> },
          { path: paths.members, element: <MembersPage /> },
          { path: paths.newMember, element: <CreateMemberPage /> },
          { path: paths.member(":id"), element: <MemberProfilePage /> },
          { path: paths.editMember(":id"), element: <EditMemberPage /> },
          { path: paths.status, element: <StatusPage /> },
          { path: paths.changePassword, element: <ChangePasswordPage /> },
          {
            path: paths.staff,
            element: (
              <RequireRole role="Owner">
                <StaffPage />
              </RequireRole>
            ),
          },
        ],
      },
    ],
  },
  { path: "*", element: <Navigate to={paths.home} replace /> },
];

export const router = createBrowserRouter(routes);
