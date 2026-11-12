import {
  Activity,
  BanknoteArrowUp,
  ChartColumn,
  ClipboardList,
  Coffee,
  Contact,
  DoorOpen,
  Dumbbell,
  Gauge,
  Globe,
  History,
  KeyRound,
  LockKeyhole,
  MessageSquareText,
  MessagesSquare,
  ReceiptText,
  ScrollText,
  Settings,
  Store,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";

import { gymRoles, type Role } from "@/features/auth/api";
import type { Tone } from "@/lib/tone";

import { paths } from "./paths";

export interface NavigationItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /**
   * Shown only to these roles. Items without them are the gym's, for the Owner and Staff
   * (`gymRoles`): a shop admin sees only the items that name `Shop` (ADR 0011).
   */
  roles?: readonly Role[];
}

/** The roles an item is shown to. */
export function rolesOf(item: NavigationItem): readonly Role[] {
  return item.roles ?? gymRoles;
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
      },
      // BUSINESS_RULES.md §16 Site users: the Owner and Staff, as the member list.
      { to: paths.siteUsers, label: "کاربران سایت", icon: Globe },
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
      { to: paths.todaySummary, label: "خلاصهٔ امروز", icon: Gauge, roles: ["Owner"] },
      { to: paths.dashboard, label: "داشبورد", icon: ChartColumn, roles: ["Owner"] },
      { to: paths.expenses, label: "هزینه‌ها", icon: Wallet, roles: ["Owner"] },
      { to: paths.payables, label: "چک و قسط", icon: BanknoteArrowUp, roles: ["Owner"] },
    ],
  },
  {
    title: "مدیریت",
    tone: "teal",
    items: [
      { to: paths.staff, label: "کارمندان", icon: Users, roles: ["Owner"] },
      { to: paths.trainers, label: "مربیان", icon: Dumbbell, roles: ["Owner"] },
      { to: paths.settings, label: "تنظیمات", icon: Settings, roles: ["Owner"] },
      {
        to: paths.smsMessages,
        label: "پیامک‌ها",
        icon: MessagesSquare,
        roles: ["Owner"],
      },
      {
        to: paths.smsSettings,
        label: "تنظیمات پیامک",
        icon: MessageSquareText,
        roles: ["Owner"],
      },
      {
        to: paths.auditLogs,
        label: "گزارش تغییرات",
        icon: ScrollText,
        roles: ["Owner"],
      },
    ],
  },
  {
    // The online shop (ADR 0011, §16): the shop admin's whole menu; the Owner reads it, Staff never.
    title: "فروشگاه آنلاین",
    tone: "pink",
    items: [
      {
        to: paths.shopContacts,
        label: "راه‌های تماس",
        icon: Store,
        roles: ["Shop", "Owner"],
      },
    ],
  },
  {
    title: "حساب و سیستم",
    items: [
      { to: paths.status, label: "وضعیت سیستم", icon: Activity, roles: ["Owner", "Staff", "Shop"] },
      {
        to: paths.changePassword,
        label: "تغییر رمز عبور",
        icon: KeyRound,
        roles: ["Owner", "Staff", "Shop"],
      },
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
