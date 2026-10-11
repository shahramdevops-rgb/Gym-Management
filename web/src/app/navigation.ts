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
  MessageSquareText,
  MessagesSquare,
  ReceiptText,
  ScrollText,
  Settings,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";

import type { Role } from "@/features/auth/api";
import type { Tone } from "@/lib/tone";

import { paths } from "./paths";

export interface NavigationItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Shown only to this role. Items without one are for every signed-in user. */
  role?: Role;
  /** A mark beside the label, so a changed or new page is found without being told. */
  badge?: NavigationBadge;
}

export interface NavigationBadge {
  text: string;
  /** Shown up to and including this gym day (ISO), then gone. Without it, shown until removed here. */
  until?: string;
}

/** A titled part of the menu. A part with nothing the user may open is not shown at all. */
export interface NavigationGroup {
  title: string;
  /**
   * The group's colour, on its menu icons and on every page it holds (roadmap 13.2e). Decoration
   * only: never green, red or amber, which mean something on every screen. Without one, the
   * group's pages keep the plain look.
   */
  tone?: Tone;
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
export const navigation: NavigationGroup[] = [
  {
    title: "پذیرش",
    tone: "sky",
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
    tone: "orange",
    items: [
      { to: paths.cafe, label: "بوفه", icon: Coffee },
      { to: paths.cafeOrders, label: "سفارش‌های بوفه", icon: ReceiptText },
      { to: paths.cafeMenu, label: "منوی بوفه", icon: ClipboardList },
    ],
  },
  {
    title: "مالی",
    tone: "violet",
    items: [
      { to: paths.dashboard, label: "داشبورد", icon: ChartColumn, role: "Owner" },
      { to: paths.expenses, label: "هزینه‌ها", icon: Wallet, role: "Owner" },
      { to: paths.payables, label: "چک و قسط", icon: BanknoteArrowUp, role: "Owner" },
    ],
  },
  {
    title: "مدیریت",
    tone: "teal",
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

/** Where a page sits in the menu: the group it belongs to and the item that leads to it. */
export interface Section {
  group: NavigationGroup;
  item: NavigationItem;
}

/**
 * The menu item a path belongs to: the one whose link is the longest whole-segment prefix of it,
 * so `/members/new` and `/members/:id/edit` are «اعضا», `/cafe/orders` is not «بوفه», and `/sms` is
 * not `/sms-settings`. `/` matches only itself, or it would hold every page. Undefined for a page
 * outside the menu.
 */
export function sectionFor(pathname: string): Section | undefined {
  let best: Section | undefined;

  for (const group of navigation) {
    for (const item of group.items) {
      const matches =
        item.to === paths.home
          ? pathname === paths.home
          : pathname === item.to || pathname.startsWith(`${item.to}/`);

      if (matches && (best === undefined || item.to.length > best.item.to.length)) {
        best = { group, item };
      }
    }
  }

  return best;
}
