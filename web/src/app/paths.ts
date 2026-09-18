/** Route paths, named once so guards, links and redirects cannot disagree about a URL. */
export const paths = {
  home: "/",
  login: "/login",
  changePassword: "/change-password",
  staff: "/staff",
  status: "/status",
  members: "/members",
  newMember: "/members/new",
  member: (id: string) => `/members/${id}`,
  editMember: (id: string) => `/members/${id}/edit`,
} as const;
