import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

import { usePageSection } from "./pageSection";

interface PageHeaderProps {
  title: ReactNode;
  /** One line under the title saying what the page is for. */
  description?: ReactNode;
  /** The page's main buttons, at the end of the title row. */
  actions?: ReactNode;
  /** The icon before the title. Without one, the page's menu item's icon, given by the frame. */
  icon?: LucideIcon;
  className?: string;
}

/**
 * The top of every page: its title (the page's one `h2`; the app's name in the header is the `h1`),
 * an optional line under it, and the page's own buttons at the far end (roadmap 13.2).
 *
 * Before the title, the page's icon in a square of its menu group's colour, the shape of the
 * dashboard's section headings (13.2e). The colour is `--tone`, set on the page by the frame; a
 * group without one gets a grey square. The icon is decoration: the title says it all.
 */
export function PageHeader({ title, description, actions, icon, className }: PageHeaderProps) {
  const section = usePageSection();
  const Icon = icon ?? section?.icon;

  return (
    <div className={cn("flex flex-wrap items-end justify-between gap-x-6 gap-y-3", className)}>
      <div className="flex min-w-0 items-center gap-3">
        {Icon !== undefined && (
          <span
            aria-hidden
            data-testid="page-header-icon"
            className={cn(
              "grid size-11 shrink-0 place-items-center rounded-xl",
              section?.tone === undefined ? "bg-muted text-muted-foreground" : "tone-soft tone-ink",
            )}
          >
            <Icon className="size-5.5" />
          </span>
        )}
        <div className="min-w-0 space-y-0.5">
          <h2 className="text-2xl leading-tight font-extrabold">{title}</h2>
          {description !== undefined && (
            <p className="text-sm text-muted-foreground">{description}</p>
          )}
        </div>
      </div>
      {actions !== undefined && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
