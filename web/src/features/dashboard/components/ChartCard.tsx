import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

import { toneStyle, type Tone } from "../tone";

interface ChartCardProps {
  title: string;
  description?: string;
  /** Beside the title, in a tinted square: decoration, so each chart is found at a glance. */
  icon: LucideIcon;
  tone: Tone;
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
  icon: Icon,
  tone,
  empty = false,
  className,
  children,
}: ChartCardProps) {
  return (
    <Card
      style={toneStyle(tone)}
      className={cn("rounded-2xl transition-shadow hover:shadow-md", className)}
    >
      <CardHeader className="grid-cols-[auto_1fr] gap-x-3">
        <span
          aria-hidden
          className="row-span-2 grid size-10 place-items-center rounded-xl tone-soft tone-ink"
        >
          <Icon className="size-5" />
        </span>
        <CardTitle className="self-end">{title}</CardTitle>
        {description !== undefined && <CardDescription>{description}</CardDescription>}
      </CardHeader>
      <CardContent>
        {empty ? (
          <div className="flex flex-col items-center gap-2 py-8 text-sm text-muted-foreground">
            <Icon aria-hidden className="size-8 opacity-40" />
            <p>در این بازه چیزی نیست.</p>
          </div>
        ) : (
          <figure aria-label={title}>{children}</figure>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * A Recharts bar's fill that fades from its colour at the top to a lighter one at its foot, put
 * inside the chart and used as `fill={`url(#${id})`}`. The colour goes in `style`, not in the
 * `stop-color` attribute, so a CSS variable is read.
 */
export function BarGradient({ id, color }: { id: string; color: string }) {
  return (
    <defs>
      <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" style={{ stopColor: color, stopOpacity: 1 }} />
        <stop offset="100%" style={{ stopColor: color, stopOpacity: 0.55 }} />
      </linearGradient>
    </defs>
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
      className="space-y-1 rounded-xl border bg-card px-3 py-2 text-sm text-card-foreground shadow-md"
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
