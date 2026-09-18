import { QueryClient } from "@tanstack/react-query";

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Two staff members at a front desk, not a live dashboard: refetching on every tab
        // switch would only add load and flicker.
        refetchOnWindowFocus: false,
        retry: 1,
      },
    },
  });
}
