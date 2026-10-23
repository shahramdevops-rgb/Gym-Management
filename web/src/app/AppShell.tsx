import * as DialogPrimitive from "@radix-ui/react-dialog";
import { LogOut, Menu, X } from "lucide-react";
import { useMemo, useState, type ReactNode } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router";

import logo from "@/assets/brand/pasargad-logo.png";
import { PageSectionContext } from "@/components/pageSection";
import { Button } from "@/components/ui/button";
import { hasRole, useCurrentUser, useLogout } from "@/features/auth/api";
import { useSessionState } from "@/features/auth/session";
import { PayablesDueAlert } from "@/features/payables/components/PayablesDueAlert";
import { SmsCreditBadge } from "@/features/sms/components/SmsCreditBadge";
import { toneStyle } from "@/lib/tone";
import { useIsPhone } from "@/lib/useIsPhone";
import { cn } from "@/lib/utils";

import { navigation, sectionFor, type NavigationGroup } from "./navigation";
import { paths } from "./paths";
import { SidebarClock } from "./SidebarClock";
import { ThemeToggle } from "./ThemeToggle";

/**
 * The page frame: the side menu on the right (the gym's logo, the clock and the menu), and beside it
 * the content under its header (the cheque alert, the SMS credit, the user, the theme and «خروج»). No frame and no
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
 *
 * On a phone (13.1a) the menu leaves the row: the header gets a «منو» button at its start, and the
 * same menu opens over the page as a drawer from the right, closing when a page is chosen.
 */
export function AppShell() {
  const state = useSessionState();
  const user = useCurrentUser();
  const logout = useLogout();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const section = useMemo(() => {
    const found = sectionFor(pathname);
    return found === undefined ? undefined : { icon: found.item.icon, tone: found.group.tone };
  }, [pathname]);

  const isPhone = useIsPhone();

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

  // The Owner reads cheques and instalments (§1) and the SMS credit (§10); a user who must change
  // their password sees only that page.
  const showOwnerAlerts = !mustChangePassword && hasRole(user.data, "Owner");

  async function signOut() {
    await logout.mutateAsync();
    navigate(paths.login, { replace: true });
  }

  return (
    // The window itself never scrolls: the page's column does (13.2i, asked by the developer).
    // Chrome draws the window's scrollbar on the right even on a right-to-left page, beside the
    // menu; a box that scrolls inside a right-to-left page has it on its left, at the far edge.
    // `h-dvh` on a phone (13.1a): there `100vh` is the height with the browser's address bar
    // hidden, so the bottom of the page would sit under the bar while it shows. The computer keeps
    // `h-screen`, as everything from `lg` up stays as it was (asked by the developer).
    <div className="flex h-screen overflow-hidden max-lg:h-dvh">
      {/* The menu stays put while the page scrolls; it scrolls on its own only on a window too
          short for it. On a phone it is the drawer instead. */}
      <aside className="hidden h-screen w-70 shrink-0 lg:block">
        <SideMenu groups={groups} />
      </aside>

      {/* `relative`: anything absolutely placed inside the page (such as a chart's text for screen
          readers) is placed in this column, so it scrolls with it and never makes the window
          taller, which would bring back the window's own scrollbar beside the menu. */}
      <div className="relative flex h-screen min-w-0 flex-1 flex-col overflow-y-auto max-lg:h-dvh">
        {/* Everything at the end (the left), asked by the developer (13.2h): the cheque alert, moved
            from the middle, and at the far left the user, the theme and «خروج» in one box. On a
            phone, «منو» at the start; the user's name and the word «خروج» give their room to it. */}
        <header className="sticky top-0 z-40 flex h-12 shrink-0 items-center justify-end gap-3 border-b bg-background/80 px-4 backdrop-blur-md lg:px-6">
          {groups.length > 0 && (
            // Open only on a phone: a drawer left open while the window grows past the breakpoint
            // would be hidden yet still modal, and the page behind it would take no click.
            <MenuDrawer open={drawerOpen && isPhone} onOpenChange={setDrawerOpen}>
              <SideMenu
                groups={groups}
                onNavigate={() => setDrawerOpen(false)}
                close={
                  <DialogPrimitive.Close asChild>
                    <Button variant="ghost" size="sm" aria-label="بستن منو" className="ms-auto">
                      <X aria-hidden />
                    </Button>
                  </DialogPrimitive.Close>
                }
              />
            </MenuDrawer>
          )}
          {showOwnerAlerts && <PayablesDueAlert />}
          {showOwnerAlerts && <SmsCreditBadge />}
          <div className="flex h-9 shrink-0 items-center gap-1 rounded-lg border bg-card sm:ps-3">
            {user.data !== undefined && (
              <span className="hidden pe-2 text-sm font-medium sm:inline">
                {user.data.fullName}
              </span>
            )}
            <ThemeToggle />
            <Button
              variant="ghost"
              size="sm"
              aria-label="خروج از حساب"
              onClick={() => void signOut()}
              disabled={logout.isPending}
            >
              <LogOut aria-hidden />
              <span className="hidden sm:inline">خروج از حساب</span>
            </Button>
          </div>
        </header>

        <main
          // A slim header and little room above the page's banner (asked by the developer, 13.2h);
          // a 16px gutter on a phone (13.1a).
          className="min-w-0 flex-1 p-4 sm:p-6 sm:pt-4 lg:p-8 lg:pt-4"
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
 * The «منو» button and the drawer it opens on a phone (13.1a): the side menu over the page, from
 * the start edge (the right). Radix's dialog does what a drawer needs: focus moves into it and stays
 * there, Escape and a tap on the dimmed page close it, and focus returns to the button. Hidden from
 * the breakpoint up, where the menu stands beside the page.
 */
function MenuDrawer({
  open,
  onOpenChange,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  children: ReactNode;
}) {
  return (
    <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <DialogPrimitive.Trigger asChild>
        {/* `me-auto`: first in the header's row, it keeps the start; the rest stay at the end. */}
        <Button variant="ghost" aria-label="منو" className="me-auto lg:hidden">
          <Menu aria-hidden />
        </Button>
      </DialogPrimitive.Trigger>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40 backdrop-blur-[2px] motion-safe:data-[state=open]:animate-overlay-in lg:hidden" />
        <DialogPrimitive.Content
          // The menu names itself («منوی اصلی»); there is no description to point at.
          aria-describedby={undefined}
          className="fixed inset-y-0 start-0 z-50 w-70 max-w-[85vw] bg-background shadow-overlay motion-safe:data-[state=open]:animate-drawer-in lg:hidden"
        >
          <DialogPrimitive.Title className="sr-only">منو</DialogPrimitive.Title>
          {children}
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}

/**
 * The side menu itself, the same beside the page and in the phone's drawer: the gym's logo and name
 * in a row as tall as the header, the clock, and the menu groups. `onNavigate` runs when a page is
 * chosen (the drawer closes then); `close` is drawn at the end of the logo row (the drawer's ✕).
 */
function SideMenu({
  groups,
  onNavigate,
  close,
}: {
  groups: NavigationGroup[];
  onNavigate?: () => void;
  close?: ReactNode;
}) {
  return (
    <div className="flex h-full flex-col">
      {/* As tall as the header beside it, so the header's bottom line runs on under it. */}
      <div className="flex h-12 shrink-0 items-center gap-2.5 border-b px-4">
        {/* White lines on transparency, so it sits on the brand blue in both themes. */}
        <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary p-0.5">
          <img src={logo} alt="" className="size-full object-contain" />
        </span>
        <div className="min-w-0 leading-tight">
          <h1 className="text-sm font-extrabold">مدیریت باشگاه</h1>
          <p className="text-xs text-muted-foreground">باشگاه پاسارگاد</p>
        </div>
        {close}
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
              className="flex items-center gap-1.5 px-3 text-xs leading-5 font-bold text-muted-foreground short:leading-4"
            >
              {group.tone !== undefined && (
                <span aria-hidden className="size-1.5 shrink-0 rounded-full bg-current tone-line" />
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
                    onClick={onNavigate}
                    className={({ isActive }) =>
                      cn(
                        // A finger-sized row in the drawer (13.1a); the desk's slim one from `lg`.
                        "flex h-10 items-center gap-2.5 rounded-lg border border-s-[3px] px-3 text-sm leading-5 font-medium transition-colors lg:h-8 lg:short:h-7",
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
                          className={cn("size-4 shrink-0", !isActive && "text-muted-foreground")}
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
  );
}

/**
 * A line beside a menu group's title, in the colour of its pages' edges (13.2h, asked by the developer with a
 * sketch, then on the canvas: "not curved, and not all straight"): it leaves the end of the title
 * (13.2j, asked with a second sketch: not from the dot, under the words), bends down once and runs
 * straight to the far edge, fading out at the very end, so the groups read as parts without a hard rule.
 * The bend is a fixed-size curve and the straight part a stretching bar, so the bend looks the same
 * after a short title and a long one. Shifted down with a transform inside the title's own line, so
 * it adds no height and the menu still fits a 900px-tall window. Decoration only.
 */
function GroupSwoosh({ muted }: { muted: boolean }) {
  return (
    <span
      aria-hidden
      className={cn(
        "pointer-events-none flex h-3.5 min-w-0 flex-1 translate-y-[5px] items-end [mask-image:linear-gradient(to_left,black_75%,transparent)]",
        // `tone-line`: the colour of the page's edges, so the line and the page beside it match
        // (13.2i, asked by the developer on «فروش بوفه»).
        muted ? "text-muted-foreground/50" : "tone-line",
      )}
    >
      {/* First in a right-to-left row: the bend, beside the title's last word. */}
      <svg viewBox="0 0 16 14" className="h-full w-4 shrink-0 overflow-visible">
        <path
          d="M15 1 C9 1 7 13.25 -0.5 13.25"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
        />
      </svg>
      <span className="h-[1.5px] flex-1 bg-current" />
    </span>
  );
}
