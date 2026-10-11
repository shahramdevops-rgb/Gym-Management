import { LogOut } from "lucide-react";
import { useMemo } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router";

import logo from "@/assets/brand/pasargad-logo.png";
import { PageSectionContext } from "@/components/pageSection";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { hasRole, useCurrentUser, useLogout } from "@/features/auth/api";
import { useSessionState } from "@/features/auth/session";
import { PayablesDueAlert } from "@/features/payables/components/PayablesDueAlert";
import { gymToday } from "@/lib/format";
import { toneStyle } from "@/lib/tone";
import { cn } from "@/lib/utils";

import { navigation, sectionFor } from "./navigation";
import { paths } from "./paths";
import { SidebarClock } from "./SidebarClock";
import { ThemeToggle } from "./ThemeToggle";

/**
 * The page frame: the side menu on the right as a floating panel (the gym's logo, the clock and the
 * menu, roadmap 13.2f), and beside it the content with its header (the cheque alert, the user, the
 * theme and «خروج») on top.
 *
 * Nothing here says "right". The menu is simply first in a row, and the document is
 * right-to-left, so it lands on the right; every spacing in it is logical (`ps-`, `start-`).
 *
 * The menu shows only what the user may open. A user who must change their password sees
 * no menu at all: there is nowhere else they can go yet. The clock above it is always there.
 *
 * Each menu group has a colour (roadmap 13.2e). The frame sets it on the page as `--tone`, and
 * hands the page its menu item's icon, so every page wears its group's colour without saying which
 * group it is in. The active item stays in the brand blue, so "where am I" is one colour.
 */
export function AppShell() {
  const state = useSessionState();
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();
  const today = gymToday();
  const { pathname } = useLocation();
  const section = useMemo(() => {
    const found = sectionFor(pathname);
    return found === undefined ? undefined : { icon: found.item.icon, tone: found.group.tone };
  }, [pathname]);

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
    <div className="flex min-h-screen">
      {/* The panel stays put while the page scrolls; its menu scrolls on its own only on a
          window too short for it. */}
      <aside className="sticky top-0 h-screen w-70 shrink-0 px-3 py-2.5 short:p-2">
        <div className="flex h-full flex-col rounded-2xl sidebar-panel">
          <div className="flex shrink-0 items-center gap-3 px-4 pt-3 pb-2.5 short:pt-2.5 short:pb-2">
            {/* White lines on transparency, so it sits on the brand blue in both themes. */}
            <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-linear-to-br from-primary to-(--tone-violet) p-1 shadow-md shadow-primary/30">
              <img src={logo} alt="" className="size-full object-contain" />
            </span>
            <div className="min-w-0 leading-tight">
              <h1 className="text-base font-extrabold">مدیریت باشگاه</h1>
              <p className="text-xs text-muted-foreground">باشگاه پاسارگاد</p>
            </div>
          </div>

          <nav
            aria-label="منوی اصلی"
            className="min-h-0 flex-1 space-y-3 overflow-y-auto px-3 pb-2 [scrollbar-color:var(--input)_transparent] [scrollbar-width:thin] short:space-y-2 short:pb-2"
          >
            <SidebarClock />
            {groups.map((group, index) => (
              <div
                key={group.title}
                className="space-y-0.5"
                style={group.tone === undefined ? undefined : toneStyle(group.tone)}
              >
                <p
                  id={`nav-group-${index}`}
                  className={cn(
                    "flex items-center gap-1.5 px-3 text-[0.7rem] leading-5 font-bold short:leading-4",
                    group.tone === undefined ? "text-muted-foreground/80" : "tone-ink",
                  )}
                >
                  {group.tone !== undefined && (
                    <span
                      aria-hidden
                      className="size-1.5 shrink-0 rounded-full tone-solid shadow-[0_0_6px_var(--tone)]"
                    />
                  )}
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
                            "relative flex h-8 items-center gap-2.5 rounded-lg px-3 text-sm leading-5 font-medium transition-colors short:h-7",
                            isActive
                              ? // The brand blue, stronger at the start, a faint ring and a bar on
                                // the start edge: "you are here" in one colour on every page.
                                "bg-linear-to-l from-(--sidebar-active-from) to-(--sidebar-active-to) font-semibold text-primary ring-1 ring-primary/25 ring-inset before:absolute before:inset-y-1.5 before:start-0 before:w-[3px] before:rounded-e-full before:bg-primary"
                              : "text-foreground/75 hover:bg-accent hover:text-foreground",
                          )
                        }
                      >
                        {({ isActive }) => (
                          <>
                            <Icon
                              className={cn(
                                "size-4 shrink-0",
                                !isActive && group.tone !== undefined && "tone-ink",
                              )}
                              aria-hidden
                            />
                            {label}
                            {/* Hidden from screen readers: the link keeps its own name, and the
                                mark is gone when its page is no longer new. */}
                            {badge !== undefined &&
                              (badge.until === undefined || today <= badge.until) && (
                                <Badge aria-hidden className="ms-auto px-1.5 text-[0.65rem]">
                                  {badge.text}
                                </Badge>
                              )}
                          </>
                        )}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-40 flex h-16 shrink-0 items-center justify-between gap-4 border-b bg-background/80 px-6 backdrop-blur-md">
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
        </header>

        <main
          className="min-w-0 flex-1 p-6 lg:p-8"
          style={section?.tone === undefined ? undefined : toneStyle(section.tone)}
        >
          <div className="mx-auto w-full max-w-screen-2xl">
            <PageSectionContext value={section}>
              <Outlet />
            </PageSectionContext>
          </div>
        </main>
      </div>
    </div>
  );
}
