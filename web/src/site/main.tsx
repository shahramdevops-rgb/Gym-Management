import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";

import "@/index.css";
import { installStaleChunkReload } from "@/lib/staleChunkReload";

import { restoreSiteSession } from "./api/siteAuthFetch";
import { captureInstallPrompt } from "./install/installPrompt";
import { siteRoutes } from "./routes";
import { registerServiceWorker } from "./serviceWorker";
import { SiteProviders } from "./SiteProviders";
import { followSystemTheme } from "./theme";

followSystemTheme();

// The browser's offer to install comes once, early: kept for the «نصب برنامه» card (15.3b).
captureInstallPrompt();
registerServiceWorker();

// The sign-up page downloads when first opened; after a release its old file is gone.
installStaleChunkReload();

// A reload forgets the in-memory access token: the cookie, if any, is turned back into a session
// before the first screen decides between «ورود» and the home screen.
void restoreSiteSession();

const router = createBrowserRouter(siteRoutes);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <SiteProviders>
      <RouterProvider router={router} />
    </SiteProviders>
  </StrictMode>,
);
