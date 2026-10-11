import { Inbox, LoaderCircle, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

/**
 * While a list or a page is loading: a turning circle and the words, in one place for every screen
 * (roadmap 13.2). The words stay, so a screen reader reads them and the circle is only decoration.
 * No `role="status"`: pages already use that role for their saved messages, and two would clash.
 */
export function LoadingState({
  children = "در حال بارگذاری…",
  className,
}: {
  children?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("flex items-center gap-2 py-6 text-sm text-muted-foreground", className)}>
      <LoaderCircle className="size-4 motion-safe:animate-spin" aria-hidden />
      <p>{children}</p>
    </div>
  );
}

/**
 * A list with nothing in it, said plainly in a quiet box rather than a stray line of grey text
 * (roadmap 13.2). An action can follow, such as the button that adds the first item.
 */
export function EmptyState({
  children,
  icon: Icon = Inbox,
  action,
  className,
}: {
  children: ReactNode;
  icon?: LucideIcon;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed bg-muted/30 px-6 py-10 text-center",
        className,
      )}
    >
      <span className="flex size-10 items-center justify-center rounded-full bg-muted text-muted-foreground">
        <Icon className="size-5" aria-hidden />
      </span>
      <p className="text-sm text-muted-foreground">{children}</p>
      {action}
    </div>
  );
}
