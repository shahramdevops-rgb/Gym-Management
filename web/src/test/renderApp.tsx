import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";

import { Providers } from "@/app/providers";
import { routes } from "@/app/router";
import { sessionStore, type AccessTokenResponse } from "@/features/auth/session";

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

  const router = createMemoryRouter(routes, { initialEntries: [url] });

  const result = render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );

  return { ...result, router };
}
