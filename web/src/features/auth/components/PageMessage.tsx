import type { ReactNode } from "react";

/** A short centred message in place of a page: loading, not allowed, and the like. */
export function PageMessage({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-[50vh] items-center justify-center p-6 text-muted-foreground">
      <p>{children}</p>
    </div>
  );
}
