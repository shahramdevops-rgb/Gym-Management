import type { LucideIcon } from "lucide-react";
import type { CSSProperties, ReactNode } from "react";

import logo from "@/assets/brand/pasargad-logo.png";
import { cn } from "@/lib/utils";

import { usePageSection } from "./pageSection";

interface PageHeroProps {
  title: ReactNode;
  /** The page's own tools (a search, a switch), at the end of the title row. */
  actions?: ReactNode;
  /** Beside the title, in the same box: a strip of the page's figures. */
  children?: ReactNode;
  /** The icon before the title. Without one, the page's menu item's icon, given by the frame. */
  icon?: LucideIcon;
  className?: string;
}

/**
 * The top of a page as one slim box in its menu group's colour (13.2h). It speaks the side menu's
 * language (asked by the developer, after the menu lost its frame and glow): like the active menu
 * item, no fill, and edged like a locker door: a faint frame and a thick start edge in the group's
 * colour (`tone-edge`, the same colour as the menu's dot and line). Inside, one row that wraps:
 * the page's icon in a tinted square and its title, its figures and tools, and the gym's logo at
 * the far end. The desk works under it all day, so it stays one line tall on a wide screen. The
 * logo is decoration.
 */
export function PageHero({ title, actions, children, icon, className }: PageHeroProps) {
  const section = usePageSection();
  const Icon = icon ?? section?.icon;

  return (
    <div
      className={cn(
        "flex flex-wrap items-center gap-x-6 gap-y-3 rounded-xl border border-s-[3px] tone-edge bg-card px-4 py-2.5",
        className,
      )}
    >
      <div className="flex min-w-0 items-center gap-3">
        {Icon !== undefined && (
          <span
            aria-hidden
            className="grid size-9 shrink-0 place-items-center rounded-lg tone-soft tone-ink"
          >
            <Icon className="size-5" />
          </span>
        )}
        <h2 className="text-xl leading-tight font-extrabold tracking-tight">{title}</h2>
      </div>
      {children !== undefined && <div className="min-w-0 flex-1">{children}</div>}
      {/* The page's tools at the far end, beside the logo (asked by the developer, 13.2i). */}
      {actions !== undefined && (
        <div className="ms-auto flex flex-wrap items-center gap-2">{actions}</div>
      )}
      {/* The logo is white lines on transparency, so it is drawn as a mask in the group's colour:
          it reads on the card in both themes. `ms-auto` keeps it at the far end even on a page
          with nothing beside the title (asked by the developer, 13.2i); after the tools, they
          already took the free room. */}
      <span
        aria-hidden
        className={cn(
          "hidden h-10 w-12 shrink-0 bg-current tone-line lg:block",
          actions === undefined && "ms-auto",
        )}
        style={logoMask}
      />
    </div>
  );
}

const logoMask: CSSProperties = {
  maskImage: `url(${logo})`,
  maskSize: "100% 100%",
  // Chrome 109 on the backup tablet knows only the prefixed name.
  WebkitMaskImage: `url(${logo})`,
  WebkitMaskSize: "100% 100%",
};
