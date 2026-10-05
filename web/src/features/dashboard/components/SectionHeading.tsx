import type { LucideIcon } from "lucide-react";

import { toneStyle, type Tone } from "../tone";

interface SectionHeadingProps {
  id: string;
  icon: LucideIcon;
  tone: Tone;
  children: string;
  /** A line beside the title, for what the section's figures are about. */
  aside?: string;
}

/** A dashboard section's title, with its icon in a tinted square. */
export function SectionHeading({ id, icon: Icon, tone, children, aside }: SectionHeadingProps) {
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1" style={toneStyle(tone)}>
      <span aria-hidden className="grid size-8 place-items-center rounded-lg tone-soft tone-ink">
        <Icon className="size-4.5" />
      </span>
      <h3 id={id} className="text-lg font-bold">
        {children}
      </h3>
      {aside !== undefined && <span className="text-sm text-muted-foreground">{aside}</span>}
    </div>
  );
}
