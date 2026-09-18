import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router/dom";

import { Providers } from "./app/providers";
import { router } from "./app/router";
import { restoreSession } from "./lib/api/authFetch";
import "./index.css";

// A reload forgets the in-memory access token. Started before the first render, so RequireAuth
// shows "loading" for the moment it takes instead of sending a logged-in user to the login page.
void restoreSession();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <Providers>
      <RouterProvider router={router} />
    </Providers>
  </StrictMode>,
);
