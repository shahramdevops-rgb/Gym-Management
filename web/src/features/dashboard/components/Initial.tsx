import { toneStyle, type Tone } from "../tone";

/**
 * A person's first letter in a tinted circle, beside their name in a list: it gives the rows a
 * face to scan by. Decoration only: the name is written next to it.
 */
export function Initial({ name, tone }: { name: string; tone: Tone }) {
  return (
    <span
      aria-hidden
      style={toneStyle(tone)}
      className="grid size-8 shrink-0 place-items-center rounded-full text-sm font-bold tone-soft tone-ink"
    >
      {name.trim().charAt(0)}
    </span>
  );
}
