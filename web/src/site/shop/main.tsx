import { DirectionProvider } from "@radix-ui/react-direction";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";

import "@/index.css";
import "./shop.css";

import { shopBase } from "./paths";
import { shopRoutes } from "./routes";

// The shop's showcase (roadmap 16.4): the second page of the site build, on the gym's public
// address under /shop. No session, no service worker and no install: a visitor reads it like any
// page of a website. The cart and the customer's sign-in (16.5) grow from here.

const queryClient = new QueryClient({
  defaultOptions: { queries: { refetchOnWindowFocus: false, retry: 1 } },
});

const router = createBrowserRouter(shopRoutes, { basename: shopBase });

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <DirectionProvider dir="rtl">
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </DirectionProvider>
  </StrictMode>,
);
