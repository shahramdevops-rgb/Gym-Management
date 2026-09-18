import { Activity } from "lucide-react";
import { NavLink, Outlet } from "react-router";

import { cn } from "@/lib/utils";

const navigation = [{ to: "/", label: "وضعیت سیستم", icon: Activity }];

/**
 * The page frame: header across the top, navigation on the right, content beside it.
 *
 * Nothing here says "right". The navigation is simply first in a row, and the document is
 * right-to-left, so it lands on the right; the border sits on its logical end (border-e).
 */
export function AppShell() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-14 items-center border-b bg-card px-6">
        <h1 className="text-lg font-bold">مدیریت باشگاه</h1>
      </header>

      <div className="flex flex-1">
        <nav aria-label="منوی اصلی" className="w-56 shrink-0 border-e bg-card p-3">
          <ul className="space-y-1">
            {navigation.map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end
                  className={({ isActive }) =>
                    cn(
                      "flex items-center gap-2 rounded-md px-3 py-2 text-sm",
                      isActive ? "bg-accent font-medium" : "hover:bg-accent/60",
                    )
                  }
                >
                  <Icon className="size-4" aria-hidden />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
