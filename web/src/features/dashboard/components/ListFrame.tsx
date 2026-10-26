import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

import { toneStyle, type Tone } from "@/lib/tone";

interface ListFrameProps {
  headingId: string;
  title: string;
  icon: LucideIcon;
  tone: Tone;
  /** How many are on the list, with its noun: «۳ نفر», «۲ مورد». */
  count: string;
  /** The rule the list follows, in one line, so the Owner knows why something is on it. */
  rule: string;
  children: ReactNode;
}

/**
 * The box every list of «نیاز به اقدام» sits in: its icon, title and count, and its rule. Edged in
 * the list's tone like a locker door (13.2i), its count an edged tag.
 */
export function ListFrame({
  headingId,
  title,
  icon: Icon,
  tone,
  count,
  rule,
  children,
}: ListFrameProps) {
  return (
    <section
      aria-labelledby={headingId}
      style={toneStyle(tone)}
      className="space-y-2 rounded-xl border border-s-[3px] tone-edge p-3"
    >
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <span
            aria-hidden
            className="grid size-7 place-items-center rounded-lg tone-soft tone-ink"
          >
            <Icon className="size-4" />
          </span>
          <h4 id={headingId} className="font-semibold">
            {title}
          </h4>
        </div>
        <span className="rounded-md border border-e-[3px] tone-edge-end px-2 py-0.5 text-xs font-bold tone-ink">
          {count}
        </span>
      </div>
      <p className="text-xs text-muted-foreground">{rule}</p>
      {children}
    </section>
  );
}
