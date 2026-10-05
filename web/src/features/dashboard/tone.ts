import type { CSSProperties } from "react";

/** The dashboard's accent hues, the --tone-* tokens of index.css. */
export type Tone =
  | "blue"
  | "orange"
  | "green"
  | "violet"
  | "teal"
  | "amber"
  | "pink"
  | "sky"
  | "red"
  | "brown"
  | "indigo"
  | "lime"
  | "cyan";

/**
 * Sets `--tone` on an element, for the `tone-soft`, `tone-ink`, `tone-solid` and `tone-bar`
 * utilities inside it to read. A custom property rather than one class per hue: the utilities stay
 * four, whatever the number of hues.
 */
export function toneStyle(tone: Tone): CSSProperties {
  return { "--tone": `var(--tone-${tone})` } as CSSProperties;
}

/** The same, for a colour that is not one of the tones (a chart's series colour). */
export function toneColorStyle(color: string): CSSProperties {
  return { "--tone": color } as CSSProperties;
}
