import { DirectionProvider } from "@radix-ui/react-direction";
import { QueryClientProvider } from "@tanstack/react-query";
import { useEffect, useState, type ReactNode } from "react";

import { clearCacheWhenUserChanges, createQueryClient } from "./queryClient";

/**
 * App-wide context. DirectionProvider matters even though <html dir="rtl"> is set: Radix
 * components (menus, sliders, tabs) compute placement and arrow-key direction in JavaScript
 * and do not read the document's dir attribute.
 */
export function Providers({ children }: { children: ReactNode }) {
  // Created once per mount rather than at module level, so tests get an isolated cache.
  const [queryClient] = useState(createQueryClient);

  useEffect(() => clearCacheWhenUserChanges(queryClient), [queryClient]);

  return (
    <DirectionProvider dir="rtl">
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </DirectionProvider>
  );
}
