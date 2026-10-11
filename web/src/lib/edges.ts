import { cn } from "@/lib/utils";

/*
 * The panel's door-like edges (13.2h, 13.2i, asked by the developer): no fill, a faint frame and one
 * thick edge, the way a locker door on the map is edged in its state's colour. Kept here so every
 * page draws them the same way.
 */

/**
 * A filter box (a date, a member, a search, a dropdown): on the page's colour, edged in the page's
 * tone with the thick edge at its end. `!`: the field's own `border-input` comes later in the
 * stylesheet and would win.
 */
export const filterEdge = "border-e-[3px] tone-edge-end! bg-background";

/**
 * A tag or a row's button in a state's colour, with the thick edge at its end, closing the row its
 * start edge opens: green for what is fine or moves on (paid, inside, check-out), red for what is
 * owed or undone, amber for what needs attention soon, grey for what is over.
 */
export const stateEdge = {
  success: "border border-success/40 border-e-[3px] border-e-success text-success",
  destructive: "border border-destructive/40 border-e-[3px] border-e-destructive text-destructive",
  warning: "border border-warning/40 border-e-[3px] border-e-warning text-warning",
  muted:
    "border border-muted-foreground/40 border-e-[3px] border-e-muted-foreground text-muted-foreground",
} as const;

/**
 * A tab or one choice of a group of buttons. The chosen one looks like the active menu item: the
 * page's tone as text, edged like a locker door. The others keep a transparent frame of the same
 * width, so choosing one does not shift the row. Tabs keep the thick edge at their start, like the
 * menu; a filter's choices at their end, like the filter boxes.
 */
export function choiceClass(chosen: boolean, edge: "start" | "end" = "start"): string {
  return cn(
    edge === "start" ? "border border-s-[3px]" : "border border-e-[3px]",
    chosen
      ? cn(
          "font-semibold tone-ink hover:bg-transparent hover:tone-ink",
          edge === "start" ? "tone-edge" : "tone-edge-end",
        )
      : "border-transparent text-foreground/75",
  );
}
