import type { LucideIcon } from "lucide-react";
import { createContext, useContext } from "react";

import type { Tone } from "@/lib/tone";

/** What the frame knows about the page it shows: its menu item's icon and its group's colour. */
export interface PageSection {
  icon: LucideIcon;
  /** Undefined for a group without a colour; the page keeps the plain look. */
  tone?: Tone;
}

/**
 * Filled by the frame (`AppShell`) from the menu, so a page never says which group it is in, and
 * the menu and the page cannot disagree (roadmap 13.2e). Undefined outside the frame or for a page
 * outside the menu.
 */
export const PageSectionContext = createContext<PageSection | undefined>(undefined);

export function usePageSection(): PageSection | undefined {
  return useContext(PageSectionContext);
}
