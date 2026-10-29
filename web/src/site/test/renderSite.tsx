import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, type RouteObject } from "react-router";

import type { components } from "@/lib/api/schema";
import { json } from "@/test/mockApi";

import { siteSessionStore, type SiteAccess, type SiteMe } from "../auth/session";
import { signUpTicketStore, type SignUpTicket } from "../auth/signUpTicket";
import { siteRoutes } from "../routes";
import { SiteProviders } from "../SiteProviders";

type SiteMemberMe = components["schemas"]["SiteMemberMe"];
type SiteTrainerMe = components["schemas"]["SiteTrainerMe"];

/** The site's routes with the lazy sign-up page already loaded, as `test/renderApp.tsx` does. */
async function loadedRoutes(table: RouteObject[]): Promise<RouteObject[]> {
  return Promise.all(
    table.map(async (route) => {
      const { lazy, children, ...rest } = route;
      const loaded = typeof lazy === "function" ? await lazy() : {};
      const loadedChildren =
        children === undefined ? {} : { children: await loadedRoutes(children) };

      return { ...rest, ...loaded, ...loadedChildren } as RouteObject;
    }),
  );
}

const testRoutes = await loadedRoutes(siteRoutes);

interface RenderSiteOptions {
  /** Signed in with this access; signed out when omitted. */
  access?: SiteAccess;
  /** A new phone's ticket, as its sign-in leaves it; set after the session, which drops tickets. */
  signUpTicket?: SignUpTicket;
}

/** Renders the site app at a URL with the real routes and providers, without a browser history. */
export function renderSite(url = "/", { access, signUpTicket }: RenderSiteOptions = {}) {
  if (access === undefined) {
    siteSessionStore.signOut();
  } else {
    siteSessionStore.signIn(access);
  }
  if (signUpTicket !== undefined) {
    signUpTicketStore.set(signUpTicket);
  }

  const router = createMemoryRouter(testRoutes, { initialEntries: [url] });

  const result = render(
    <SiteProviders>
      <RouterProvider router={router} />
    </SiteProviders>,
  );

  return { ...result, router };
}

export const activeMember: SiteMemberMe = {
  memberId: "0199a000-0000-7000-8000-000000000101",
  fullName: "علی محمدی",
  phoneNumber: "+989121234567",
  birthDate: "1995-03-21",
  isActive: true,
};

export const trainer: SiteTrainerMe = {
  trainerId: "0199a000-0000-7000-8000-000000000201",
  fullName: "رضا کریمی",
  phoneNumber: "+989351234567",
};

/** A sign-in's answer for `me`; the token is not a real JWT, the API is never reached. */
export function siteAccess(me: Partial<SiteMe> = {}): SiteAccess {
  return {
    accessToken: "site-access-token",
    expiresAt: "2026-10-28T10:15:00Z",
    me: { member: activeMember, trainer: null, ...me },
  };
}

/** «مربی من» and «شاگردان من» with nothing in them, for a test about something else on the home. */
export const emptyCoaching = {
  "GET /api/site/coaching/my-trainer": () => json(200, { trainer: null, request: null }),
  "GET /api/site/coaching/students": () => json(200, { requests: [], students: [] }),
};

const emptyPage = { items: [], page: 1, pageSize: 10, totalCount: 0 };

/** The member's records with nothing in them (15.5), for a test about something else. */
export const emptyRecords = {
  "GET /api/site/me/details": () =>
    json(200, {
      fullName: activeMember.fullName,
      phoneNumber: activeMember.phoneNumber,
      birthDate: activeMember.birthDate,
      joinedAt: "2026-09-01T08:00:00Z",
      signedUpAt: "2026-10-28T09:00:00Z",
    }),
  "GET /api/site/me/plan": () => json(200, { current: null, next: null }),
  "GET /api/site/me/debt": () => json(200, { total: 0, items: [] }),
  "GET /api/site/me/visits": () => json(200, emptyPage),
  "GET /api/site/me/plans": () => json(200, emptyPage),
  "GET /api/site/me/payments": () => json(200, emptyPage),
  "GET /api/site/me/cafe-orders": () => json(200, emptyPage),
};

/** Everything the home reads, empty. */
export const emptyHome = { ...emptyCoaching, ...emptyRecords };
