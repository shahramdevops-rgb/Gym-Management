import {
  Activity,
  ClipboardList,
  Coffee,
  Contact,
  DoorOpen,
  KeyRound,
  LockKeyhole,
  LogOut,
  ReceiptText,
  Settings,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import { NavLink, Outlet, useNavigate } from "react-router";

import { Button } from "@/components/ui/button";
import { hasRole, useCurrentUser, useLogout, type Role } from "@/features/auth/api";
import { useSessionState } from "@/features/auth/session";
import { cn } from "@/lib/utils";

import { paths } from "./paths";
import { SidebarClock } from "./SidebarClock";

interface NavigationItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Shown only to this role. Items without one are for every signed-in user. */
  role?: Role;
}

/**
 * The locker map comes first: it is where every check-in happens, for Staff and the Owner alike
 * (BUSINESS_RULES.md §6, §7). Finding a member is on the member list, which checks nobody in.
 */
const navigation: NavigationItem[] = [
  { to: paths.home, label: "ورود با کمد", icon: LockKeyhole },
  { to: paths.attendance, label: "داخل باشگاه", icon: DoorOpen },
  { to: paths.members, label: "اعضا", icon: Contact },
  { to: paths.cafe, label: "بوفه", icon: Coffee },
  { to: paths.cafeOrders, label: "سفارش‌های بوفه", icon: ReceiptText },
  { to: paths.cafeMenu, label: "منوی بوفه", icon: ClipboardList },
  { to: paths.expenses, label: "هزینه‌ها", icon: Wallet, role: "Owner" },
  { to: paths.staff, label: "کارمندان", icon: Users, role: "Owner" },
  { to: paths.settings, label: "تنظیمات", icon: Settings, role: "Owner" },
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
 * no menu at all: there is nowhere else they can go yet. The clock above it is always there.
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
        <nav aria-label="منوی اصلی" className="w-56 shrink-0 space-y-3 border-e bg-card p-3">
          <SidebarClock />
          <ul className="space-y-1">
            {items.map(({ to, label, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  // "/" would otherwise match every page, and "/cafe" every cafe page; "اعضا"
                  // should stay lit on a profile.
                  end={to === paths.home || to === paths.cafe}
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
