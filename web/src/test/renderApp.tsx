import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";

import { Providers } from "@/app/providers";
import { routes } from "@/app/router";

/** Renders the real route table and providers at a URL, without a browser history. */
export function renderApp(url = "/") {
  const router = createMemoryRouter(routes, { initialEntries: [url] });

  return render(
    <Providers>
      <RouterProvider router={router} />
    </Providers>,
  );
}
