import * as React from "react";

import { cn } from "@/lib/utils";

/**
 * shadcn/ui's Table: plain table elements with one look, so every list in the app reads the same
 * (roadmap 13.2). It scrolls sideways inside its own box when it is wider than the page, never the
 * page itself.
 *
 * On a phone (below `lg`, 13.1b) every row is a card instead: the headings are hidden, each cell
 * takes a line of its own, and a cell given a `label` shows it at the line's start with its value
 * at the end. A cell without one (the row's title, its buttons) takes the whole line. Every phone
 * rule is a `max-lg:` class, so the computer's table is untouched.
 */
function Table({ className, ...props }: React.ComponentProps<"table">) {
  return (
    <div
      data-slot="table-container"
      className="relative w-full overflow-x-auto max-lg:overflow-visible"
    >
      <table
        data-slot="table"
        className={cn("w-full caption-bottom text-sm max-lg:block", className)}
        {...props}
      />
    </div>
  );
}

function TableHeader({ className, ...props }: React.ComponentProps<"thead">) {
  return (
    <thead
      data-slot="table-header"
      className={cn("[&_tr]:border-b [&_tr]:hover:bg-transparent max-lg:hidden", className)}
      {...props}
    />
  );
}

function TableBody({ className, ...props }: React.ComponentProps<"tbody">) {
  return (
    <tbody
      data-slot="table-body"
      // The last row's missing border is the computer's alone: on a phone it is a card's frame.
      className={cn(
        "max-lg:flex max-lg:flex-col max-lg:gap-2 lg:[&_tr:last-child]:border-0",
        className,
      )}
      {...props}
    />
  );
}

/**
 * On a phone, how a row that is part of another row's card joins it: `below` for a row whose
 * card goes on in the rows after it, `above` for the last row of such a card, `both` for one in
 * the middle.
 */
type RowJoin = "below" | "above" | "both";

const phoneJoin: Record<RowJoin, string> = {
  below: "max-lg:rounded-b-none max-lg:border-b-0 max-lg:pb-1",
  above: "max-lg:-mt-2 max-lg:rounded-t-none max-lg:border-t-0 max-lg:pt-0",
  both: "max-lg:-mt-2 max-lg:rounded-none max-lg:border-y-0 max-lg:py-0",
};

/**
 * On a phone, a card framed like the desk panel and the active menu item (`tone-edge`): the thick
 * edge at its start in the page's tone, as the computer's row has on its first cell.
 *
 * `joins` keeps rows that belong together in one card: a payment's items under its heading, a
 * cafe order's lines under the order, the edit panel under its row. A row joining the one above
 * it is a line of that card, its cells side by side (an item and its price), and a cell spanning
 * the whole table (the edit panel) takes the line's full width.
 */
function TableRow({
  className,
  joins,
  ...props
}: React.ComponentProps<"tr"> & { joins?: RowJoin }) {
  return (
    <tr
      data-slot="table-row"
      className={cn(
        "border-b transition-colors hover:bg-muted/50",
        "max-lg:flex max-lg:flex-col max-lg:rounded-lg max-lg:border max-lg:border-s-[3px] max-lg:px-3 max-lg:py-2 max-lg:tone-edge",
        joins !== undefined && phoneJoin[joins],
        (joins === "above" || joins === "both") &&
          "max-lg:flex-row max-lg:flex-wrap max-lg:items-center max-lg:justify-between max-lg:gap-x-3 max-lg:[&>td[colspan]]:w-full",
        className,
      )}
      {...props}
    />
  );
}

function TableHead({ className, ...props }: React.ComponentProps<"th">) {
  return (
    <th
      data-slot="table-head"
      className={cn(
        "h-10 px-3 text-start align-middle text-xs font-semibold whitespace-nowrap text-muted-foreground",
        className,
      )}
      {...props}
    />
  );
}

/**
 * `label` is what the cell's heading says, shown before its value on a phone only; on a computer
 * the heading above the column says it. An empty cell is left out of a phone's card.
 */
function TableCell({
  className,
  label,
  ...props
}: React.ComponentProps<"td"> & { label?: string }) {
  return (
    <td
      data-slot="table-cell"
      data-label={label}
      className={cn(
        "px-3 py-2.5 align-middle",
        "max-lg:block max-lg:px-0 max-lg:py-1 max-lg:empty:hidden",
        // The row's start edge is the card's on a phone (`tone-row-edge` draws it as `::before`).
        label === undefined && "max-lg:before:hidden",
        label !== undefined &&
          "max-lg:flex max-lg:flex-wrap max-lg:items-center max-lg:gap-x-1 max-lg:gap-y-0.5 max-lg:before:me-auto max-lg:before:pe-2 max-lg:before:text-xs max-lg:before:text-muted-foreground max-lg:before:content-[attr(data-label)]",
        className,
      )}
      {...props}
    />
  );
}

export { Table, TableHeader, TableBody, TableRow, TableHead, TableCell };
