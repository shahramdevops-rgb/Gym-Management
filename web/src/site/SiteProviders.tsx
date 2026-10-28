import { DirectionProvider } from "@radix-ui/react-direction";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";

import { siteMeQueryKey } from "./auth/api";
import { siteSessionStore, tokenSiteAccount } from "./auth/session";

function createSiteQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { refetchOnWindowFocus: false, retry: 1 } },
  });
}

/**
 * The site app's context, as the panel's `Providers`: right-to-left for Radix, and a query cache
 * emptied on every sign-out or change of person, so a phone handed to someone else never shows the
 * last person's data. Every new token carries «me» as the database says now (the refresh reads the
 * roles again, BUSINESS_RULES.md §15), and it replaces the cached one: a trainer stopped while the
 * app stays open sees it within the token's 15 minutes, without a reload.
 */
export function SiteProviders({ children }: { children: ReactNode }) {
  // Created once per mount rather than at module level, so tests get an isolated cache.
  const [queryClient] = useState(createSiteQueryClient);

  useEffect(() => {
    const accountOf = () => {
      const state = siteSessionStore.getState();
      return state.status === "signedIn" ? tokenSiteAccount(state.access.accessToken) : undefined;
    };

    let previousAccount = accountOf();

    return siteSessionStore.subscribe(() => {
      const state = siteSessionStore.getState();
      const account = accountOf();

      if (state.status === "signedOut" || account !== previousAccount) {
        queryClient.clear();
      }
      if (state.status === "signedIn") {
        queryClient.setQueryData(siteMeQueryKey, state.access.me);
      }

      previousAccount = account;
    });
  }, [queryClient]);

  return (
    <DirectionProvider dir="rtl">
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </DirectionProvider>
  );
}
