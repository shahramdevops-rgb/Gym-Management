import {
  Activity,
  Contact,
  DoorOpen,
  KeyRound,
  LockKeyhole,
  LogOut,
  Search,
  Tickets,
  Users,
  type LucideIcon,
} from "lucide-react";
import { NavLink, Outlet, useNavigate } from "react-router";

import { Button } from "@/components/ui/button";
import { hasRole, useCurrentUser, useLogout, type Role } from "@/features/auth/api";
import { useSessionState } from "@/features/auth/session";
import { cn } from "@/lib/utils";

import { paths } from "./paths";

interface NavigationItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Shown only to this role. Items without one are for every signed-in user. */
  role?: Role;
}

const navigation: NavigationItem[] = [
  { to: paths.home, label: "جستجو", icon: Search },
  { to: paths.members, label: "اعضا", icon: Contact },
  { to: paths.attendance, label: "داخل باشگاه", icon: DoorOpen },
  { to: paths.plans, label: "پلن‌ها", icon: Tickets, role: "Owner" },
  { to: paths.lockers, label: "کمدها", icon: LockKeyhole, role: "Owner" },
  { to: paths.staff, label: "کارمندان", icon: Users, role: "Owner" },
  { to: paths.status, label: "وضعیت سیستم", icon: Activity },
  { to: paths.changePassword, label: "تغییر رمز عبور", icon: KeyRound },
];

/**
 * The page frame: header across the top, navigation on the right, content beside it.
 *
 * Nothing here says "right". The navigation is simply first in a row, and the document is
 * right-to-left, so it lands on the right; the border sits on its logical end (border-e).
 *
 * The menu shows only what the user may open. A user who must change their password sees
 * no menu at all: there is nowhere else they can go yet.
 */
export function AppShell() {
  const state = useSessionState();
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();

  const mustChangePassword = state.status === "signedIn" && state.session.mustChangePassword;
  const items = mustChangePassword
    ? []
    : navigation.filter((item) => item.role === undefined || hasRole(user.data, item.role));

  async function signOut() {
    await logout.mutateAsync();
    navigate(paths.login, { replace: true });
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-14 items-center justify-between gap-4 border-b bg-card px-6">
        <h1 className="text-lg font-bold">مدیریت باشگاه</h1>
        <div className="flex items-center gap-3">
          {user.data !== undefined && (
            <span className="text-sm text-muted-foreground">{user.data.fullName}</span>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void signOut()}
            disabled={logout.isPending}
          >
            <LogOut aria-hidden />
            خروج
          </Button>
        </div>
      </header>

      <div className="flex flex-1">
        <nav aria-label="منوی اصلی" className="w-56 shrink-0 border-e bg-card p-3">
          <ul className="space-y-1">
            {items.map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  // "/" would otherwise match every page; "اعضا" should stay lit on a profile.
                  end={to === paths.home}
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
