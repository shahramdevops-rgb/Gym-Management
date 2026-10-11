import {
  Activity,
  BanknoteArrowUp,
  ChartColumn,
  ClipboardList,
  Coffee,
  Contact,
  DoorOpen,
  History,
  KeyRound,
  LockKeyhole,
  LogOut,
  MessageSquareText,
  MessagesSquare,
  ReceiptText,
  ScrollText,
  Settings,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import { NavLink, Outlet, useNavigate } from "react-router";

import logo from "@/assets/brand/pasargad-logo.png";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { hasRole, useCurrentUser, useLogout, type Role } from "@/features/auth/api";
import { useSessionState } from "@/features/auth/session";
import { PayablesDueAlert } from "@/features/payables/components/PayablesDueAlert";
import { gymToday } from "@/lib/format";
import { cn } from "@/lib/utils";

import { paths } from "./paths";
import { SidebarClock } from "./SidebarClock";
import { ThemeToggle } from "./ThemeToggle";

interface NavigationItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Shown only to this role. Items without one are for every signed-in user. */
  role?: Role;
  /** A mark beside the label, so a changed or new page is found without being told. */
  badge?: NavigationBadge;
}

interface NavigationBadge {
  text: string;
  /** Shown up to and including this gym day (ISO), then gone. Without it, shown until removed here. */
  until?: string;
}

/** A titled part of the menu. A part with nothing the user may open is not shown at all. */
interface NavigationGroup {
  title: string;
  items: NavigationItem[];
}

/**
 * Released on 1405/07/18: «اعضا» lists by latest visit (6.5.34) and «گزارش تغییرات» is new (11.1).
 * Marked through 1405/07/25 (asked by the developer).
 */
const releasedOn18MehrUntil = "2026-10-17";

/**
 * The SMS pages are on the server, but nothing is really sent until the gym's line is registered
 * with Kavenegar. Marked until SMS goes live; remove it in task 10.6 (asked by the developer).
 */
const smsTestBadge: NavigationBadge = { text: "آزمایشی" };

/**
 * The locker map comes first: it is where every check-in happens, for Staff and the Owner alike
 * (BUSINESS_RULES.md §6, §7). Finding a member is on the member list, which checks nobody in.
 *
 * The items are grouped by the work they belong to (roadmap 13.2), so sixteen links read as five
 * short lists; the order inside each group is the order the menu always had.
 */
const navigation: NavigationGroup[] = [
  {
    title: "پذیرش",
    items: [
      { to: paths.home, label: "ورود با کمد", icon: LockKeyhole },
      { to: paths.attendance, label: "داخل باشگاه", icon: DoorOpen },
      { to: paths.history, label: "تاریخچه", icon: History },
      {
        to: paths.members,
        label: "اعضا",
        icon: Contact,
        badge: { text: "به‌روز شد", until: releasedOn18MehrUntil },
      },
    ],
  },
  {
    title: "فروش بوفه",
    items: [
      { to: paths.cafe, label: "بوفه", icon: Coffee },
      { to: paths.cafeOrders, label: "سفارش‌های بوفه", icon: ReceiptText },
      { to: paths.cafeMenu, label: "منوی بوفه", icon: ClipboardList },
    ],
  },
  {
    title: "مالی",
    items: [
      { to: paths.dashboard, label: "داشبورد", icon: ChartColumn, role: "Owner" },
      { to: paths.expenses, label: "هزینه‌ها", icon: Wallet, role: "Owner" },
      { to: paths.payables, label: "چک و قسط", icon: BanknoteArrowUp, role: "Owner" },
    ],
  },
  {
    title: "مدیریت",
    items: [
      { to: paths.staff, label: "کارمندان", icon: Users, role: "Owner" },
      { to: paths.settings, label: "تنظیمات", icon: Settings, role: "Owner" },
      {
        to: paths.smsMessages,
        label: "پیامک‌ها",
        icon: MessagesSquare,
        role: "Owner",
        badge: smsTestBadge,
      },
      {
        to: paths.smsSettings,
        label: "تنظیمات پیامک",
        icon: MessageSquareText,
        role: "Owner",
        badge: smsTestBadge,
      },
      {
        to: paths.auditLogs,
        label: "گزارش تغییرات",
        icon: ScrollText,
        role: "Owner",
        badge: { text: "جدید", until: releasedOn18MehrUntil },
      },
    ],
  },
  {
    title: "حساب و سیستم",
    items: [
      { to: paths.status, label: "وضعیت سیستم", icon: Activity },
      { to: paths.changePassword, label: "تغییر رمز عبور", icon: KeyRound },
    ],
  },
];

/**
 * The page frame: header across the top, navigation on the right, content beside it.
 *
 * Nothing here says "right". The navigation is simply first in a row, and the document is
 * right-to-left, so it lands on the right; the border sits on its logical end (border-e). The
 * header's first cell is as wide as the menu, so the logo and the menu read as one column.
 *
 * The menu shows only what the user may open. A user who must change their password sees
 * no menu at all: there is nowhere else they can go yet. The clock above it is always there.
 */
export function AppShell() {
  const state = useSessionState();
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();
  const today = gymToday();

  const mustChangePassword = state.status === "signedIn" && state.session.mustChangePassword;
  const groups = mustChangePassword
    ? []
    : navigation
        .map((group) => ({
          ...group,
          items: group.items.filter(
            (item) => item.role === undefined || hasRole(user.data, item.role),
          ),
        }))
        .filter((group) => group.items.length > 0);

  // The Owner reads cheques and instalments (§1); a user who must change their password sees only
  // that page.
  const showPayablesAlert = !mustChangePassword && hasRole(user.data, "Owner");

  async function signOut() {
    await logout.mutateAsync();
    navigate(paths.login, { replace: true });
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="sticky top-0 z-40 flex h-16 items-center border-b bg-card/85 backdrop-blur-md">
        <div className="flex h-full w-64 shrink-0 items-center gap-3 border-e px-5">
          {/* White lines on transparency, so it sits on the brand blue in both themes. */}
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary p-1 shadow-sm shadow-primary/30">
            <img src={logo} alt="" className="size-full object-contain" />
          </span>
          <div className="min-w-0 leading-tight">
            <h1 className="text-base font-extrabold">مدیریت باشگاه</h1>
            <p className="text-xs text-muted-foreground">باشگاه پاسارگاد</p>
          </div>
        </div>
        <div className="flex min-w-0 flex-1 items-center justify-between gap-4 px-6">
          <div className="flex min-w-0 flex-1 justify-center">
            {showPayablesAlert && <PayablesDueAlert />}
          </div>
          <div className="flex items-center gap-1">
            {user.data !== undefined && (
              <span className="flex items-center gap-2 pe-2 text-sm font-medium">
                <span
                  aria-hidden
                  className="flex size-8 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary"
                >
                  {user.data.fullName.trim().charAt(0)}
                </span>
                <span>{user.data.fullName}</span>
              </span>
            )}
            <ThemeToggle />
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
        </div>
      </header>

      <div className="flex flex-1">
        <nav
          aria-label="منوی اصلی"
          className="sticky top-16 h-[calc(100vh-4rem)] w-64 shrink-0 space-y-3 overflow-y-auto border-e bg-sidebar p-3 [scrollbar-color:var(--input)_transparent] [scrollbar-width:thin] short:space-y-2 short:py-2"
        >
          <SidebarClock />
          {groups.map((group, index) => (
            <div key={group.title} className="space-y-0.5">
              <p
                id={`nav-group-${index}`}
                className="px-3 text-[0.7rem] leading-5 font-semibold text-muted-foreground/80 short:leading-4"
              >
                {group.title}
              </p>
              <ul aria-labelledby={`nav-group-${index}`} className="space-y-px">
                {group.items.map(({ to, label, icon: Icon, badge }) => (
                  <li key={to}>
                    <NavLink
                      to={to}
                      // "/" would otherwise match every page, and "/cafe" every cafe page; "اعضا"
                      // should stay lit on a profile.
                      end={to === paths.home || to === paths.cafe}
                      className={({ isActive }) =>
                        cn(
                          "flex h-8 items-center gap-2.5 rounded-lg px-3 text-sm leading-5 font-medium transition-colors short:h-7",
                          isActive
                            ? "bg-primary/10 text-primary"
                            : "text-foreground/75 hover:bg-accent hover:text-foreground",
                        )
                      }
                    >
                      <Icon className="size-4 shrink-0" aria-hidden />
                      {label}
                      {/* Hidden from screen readers: the link keeps its own name, and the mark is
                          gone when its page is no longer new. */}
                      {badge !== undefined &&
                        (badge.until === undefined || today <= badge.until) && (
                          <Badge aria-hidden className="ms-auto px-1.5 text-[0.65rem]">
                            {badge.text}
                          </Badge>
                        )}
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </nav>

        <main className="min-w-0 flex-1 p-6 lg:p-8">
          <div className="mx-auto w-full max-w-screen-2xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
