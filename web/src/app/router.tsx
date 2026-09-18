import { createBrowserRouter } from "react-router";

import { StatusPage } from "@/features/status/pages/StatusPage";

import { AppShell } from "./AppShell";

/** Route table. Every page renders inside AppShell's <Outlet />. */
export const routes = [
  {
    element: <AppShell />,
    children: [{ path: "/", element: <StatusPage /> }],
  },
];

export const router = createBrowserRouter(routes);
