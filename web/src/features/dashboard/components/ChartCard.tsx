import type { ReactNode } from "react";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

interface ChartCardProps {
  title: string;
  description?: string;
  /** Shown instead of the chart when there is nothing to draw: an empty chart reads as broken. */
  empty?: boolean;
  className?: string;
  children: ReactNode;
}

/**
 * The frame every dashboard chart sits in: a title that says what is plotted (a single series
 * needs no legend box), a line under it for the unit or the rule, and the chart.
 */
export function ChartCard({
  title,
  description,
  empty = false,
  className,
  children,
}: ChartCardProps) {
  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        {description !== undefined && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent>
        {empty ? (
          <p className="py-8 text-center text-sm text-muted-foreground">در این بازه چیزی نیست.</p>
        ) : (
          <figure aria-label={title}>{children}</figure>
        )}
      </CardContent>
    </Card>
  );
}

/** A row of a chart's tooltip. */
export interface TooltipRow {
  name: string;
  value: string;
  /** The series colour, as a swatch beside the text: the text itself stays in text ink. */
  color?: string;
}

interface ChartTooltipProps {
  active: boolean;
  title: ReactNode;
  rows: TooltipRow[];
}

/**
 * What a chart shows on hover: the exact figures, through `formatMoney` and friends, where the
 * axis only had room for a rounded one. Drawn by hand rather than Recharts' default box, which
 * sets its own colours and lays its rows out left to right.
 */
export function ChartTooltip({ active, title, rows }: ChartTooltipProps) {
  if (!active || rows.length === 0) {
    return null;
  }

  return (
    <div
      dir="rtl"
      className="space-y-1 rounded-md border bg-card px-3 py-2 text-sm text-card-foreground shadow-md"
    >
      <p className="font-medium">{title}</p>
      {rows.map((row) => (
        <p key={row.name} className="flex items-center gap-2">
          {row.color !== undefined && (
            <span aria-hidden className="size-2.5 rounded-sm" style={{ background: row.color }} />
          )}
          <span className="text-muted-foreground">{row.name}:</span>
          <span className="font-medium">{row.value}</span>
        </p>
      ))}
    </div>
  );
}
