import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, type RouteObject } from "react-router";

import { Providers } from "@/app/providers";
import { routes } from "@/app/router";
import { sessionStore, type AccessTokenResponse } from "@/features/auth/session";

/**
 * The route table with every lazy page already loaded (task 11.3). In the app, every page but the
 * board downloads when first opened, so its route renders a moment later; the tests are about
 * what a page does, and were written for pages that render at once. Each route keeps everything
 * else it has, so the elements are the ones the app renders.
 */
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

const testRoutes = await loadedRoutes(routes);

interface RenderAppOptions {
  /** Signed in with this session; signed out when omitted. */
  session?: AccessTokenResponse;
}

/** Renders the real route table and providers at a URL, without a browser history. */
export function renderApp(url = "/", { session }: RenderAppOptions = {}) {
  if (session === undefined) {
    sessionStore.signOut();
  } else {
    sessionStore.signIn(session);
  }

  const router = createMemoryRouter(testRoutes, { initialEntries: [url] });

  const result = render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );

  return { ...result, router };
}
