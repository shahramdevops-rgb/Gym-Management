import { LogOut } from "lucide-react";
import { useMemo } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router";

import logo from "@/assets/brand/pasargad-logo.png";
import { PageSectionContext } from "@/components/pageSection";
import { Button } from "@/components/ui/button";
import { hasRole, useCurrentUser, useLogout } from "@/features/auth/api";
import { useSessionState } from "@/features/auth/session";
import { PayablesDueAlert } from "@/features/payables/components/PayablesDueAlert";
import { toneStyle } from "@/lib/tone";
import { cn } from "@/lib/utils";

import { navigation, sectionFor } from "./navigation";
import { paths } from "./paths";
import { SidebarClock } from "./SidebarClock";
import { ThemeToggle } from "./ThemeToggle";

/**
 * The page frame: the side menu on the right (the gym's logo, the clock and the menu), and beside it
 * the content under its header (the cheque alert, the user, the theme and «خروج»). No frame and no
 * glow (13.2h, asked by the developer: the floating menu did not belong to the page beside it): the
 * menu sits on the page's own background with no line beside it, and the header's bottom line runs on
 * under the logo, so the two read as one frame.
 *
 * Nothing here says "right". The menu is simply first in a row, and the document is
 * right-to-left, so it lands on the right; every spacing in it is logical (`ps-`, `start-`).
 *
 * The menu shows only what the user may open. A user who must change their password sees
 * no menu at all: there is nowhere else they can go yet. The clock above it is always there.
 *
 * Each menu group has a colour (roadmap 13.2e). The frame sets it on the page as `--tone`, and
 * hands the page its menu item's icon, so every page wears its group's colour without saying which
 * group it is in. The menu itself stays grey but for each group's dot and line; the active item takes
 * its group's colour, the same as the page's banner, so the menu points at the page it opened.
 */
export function AppShell() {
  const state = useSessionState();
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();
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
      {/* The menu stays put while the page scrolls; it scrolls on its own only on a window too
          short for it. */}
      <aside className="sticky top-0 h-screen w-70 shrink-0">
        <div className="flex h-full flex-col">
          {/* As tall as the header beside it, so the header's bottom line runs on under it. */}
          <div className="flex h-12 shrink-0 items-center gap-2.5 border-b px-4">
            {/* White lines on transparency, so it sits on the brand blue in both themes. */}
            <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary p-0.5">
              <img src={logo} alt="" className="size-full object-contain" />
            </span>
            <div className="min-w-0 leading-tight">
              <h1 className="text-sm font-extrabold">مدیریت باشگاه</h1>
              <p className="text-[0.7rem] text-muted-foreground">باشگاه پاسارگاد</p>
            </div>
          </div>

          <nav
            aria-label="منوی اصلی"
            className="min-h-0 flex-1 space-y-3 overflow-y-auto px-3 pt-3 pb-2 [scrollbar-color:var(--input)_transparent] [scrollbar-width:thin] short:space-y-2 short:pb-2"
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
                    "relative flex items-center gap-1.5 px-3 text-[0.7rem] leading-5 font-bold short:leading-4",
                    group.tone === undefined ? "text-muted-foreground/80" : "text-muted-foreground",
                  )}
                >
                  {group.tone !== undefined && (
                    <span aria-hidden className="size-1.5 shrink-0 rounded-full tone-solid" />
                  )}
                  {group.title}
                  <GroupSwoosh muted={group.tone === undefined} />
                </p>
                <ul aria-labelledby={`nav-group-${index}`} className="space-y-px">
                  {group.items.map(({ to, label, icon: Icon }) => (
                    <li key={to}>
                      <NavLink
                        to={to}
                        // "/" would otherwise match every page, and "/cafe" every cafe page; "اعضا"
                        // should stay lit on a profile.
                        end={to === paths.home || to === paths.cafe}
                        className={({ isActive }) =>
                          cn(
                            "flex h-8 items-center gap-2.5 rounded-lg border border-s-[3px] px-3 text-sm leading-5 font-medium transition-colors short:h-7",
                            isActive
                              ? // The group's colour (grey in «حساب و سیستم») as text, and edged like a locker
                                // door: a faint frame and a thick start edge in that colour (`tone-edge`).
                                "font-semibold tone-ink tone-edge"
                              : "border-transparent text-foreground/75 hover:bg-accent hover:text-foreground",
                          )
                        }
                      >
                        {({ isActive }) => (
                          <>
                            <Icon
                              className={cn(
                                "size-4 shrink-0",
                                !isActive && "text-muted-foreground",
                              )}
                              aria-hidden
                            />
                            {label}
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
        {/* Everything at the end (the left), asked by the developer (13.2h): the cheque alert, moved
            from the middle, and at the far left the user, the theme and «خروج» in one box. */}
        <header className="sticky top-0 z-40 flex h-12 shrink-0 items-center justify-end gap-3 border-b bg-background/80 px-6 backdrop-blur-md">
          {showPayablesAlert && <PayablesDueAlert />}
          <div className="flex h-9 shrink-0 items-center gap-1 rounded-lg border bg-card ps-3">
            {user.data !== undefined && (
              <span className="pe-2 text-sm font-medium">{user.data.fullName}</span>
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
          // A slim header and little room above the page's banner (asked by the developer, 13.2h).
          className="min-w-0 flex-1 p-6 pt-4 lg:p-8 lg:pt-4"
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

/**
 * A line under a menu group's title, in the group's colour (13.2h, asked by the developer with a
 * sketch, then on the canvas: "not curved, and not all straight"): it leaves the title's dot,
 * bends down once, and from in front of the group's first item runs straight to the far edge,
 * fading out at the very end, so the groups read as parts without a hard rule.
 * Absolutely placed in the title's own line, so it adds no height and the menu still fits a
 * 900px-tall window. Decoration only.
 */
function GroupSwoosh({ muted }: { muted: boolean }) {
  return (
    <svg
      aria-hidden
      viewBox="0 0 200 12"
      preserveAspectRatio="none"
      className={cn(
        "pointer-events-none absolute inset-x-3 top-[calc(50%+3px)] h-3 [mask-image:linear-gradient(to_left,black_75%,transparent)]",
        muted ? "text-muted-foreground/50" : "text-(--tone)",
      )}
    >
      {/* Drawn from the right, where the dot is in a right-to-left menu. */}
      <path
        d="M197 0.5 C186 0.5 178 9 164 9 L0 9"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
