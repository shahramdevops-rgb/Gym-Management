/** Route paths, named once so guards, links and redirects cannot disagree about a URL. */
export const paths = {
  /** The locker map, "ورود با کمد": the first screen for everyone (BUSINESS_RULES.md §7). */
  home: "/",
  login: "/login",
  changePassword: "/change-password",
  staff: "/staff",
  status: "/status",
  members: "/members",
  newMember: "/members/new",
  member: (id: string) => `/members/${id}`,
  editMember: (id: string) => `/members/${id}/edit`,
  settings: "/settings",
  attendance: "/attendance",
  cafe: "/cafe",
  cafeOrders: "/cafe/orders",
  cafeMenu: "/cafe/menu",
  expenses: "/expenses",
} as const;
