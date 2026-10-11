import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

interface PageHeaderProps {
  title: ReactNode;
  /** One line under the title saying what the page is for. */
  description?: ReactNode;
  /** The page's main buttons, at the end of the title row. */
  actions?: ReactNode;
  className?: string;
}

/**
 * The top of every page: its title (the page's one `h2`; the app's name in the header is the `h1`),
 * an optional line under it, and the page's own buttons at the far end (roadmap 13.2).
 */
export function PageHeader({ title, description, actions, className }: PageHeaderProps) {
  return (
    <div className={cn("flex flex-wrap items-end justify-between gap-x-6 gap-y-3", className)}>
      <div className="min-w-0 space-y-0.5">
        <h2 className="text-2xl leading-tight font-extrabold">{title}</h2>
        {description !== undefined && (
          <p className="text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      {actions !== undefined && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
