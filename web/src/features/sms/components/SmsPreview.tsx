import { useId, type ReactNode } from "react";

interface SmsPreviewProps {
  /** The message as it reaches the phone; `undefined` while it loads or if it could not be read. */
  text: ReactNode | undefined;
  /** What sits under the bubble: the parts, and that the name and figures are examples. */
  caption: ReactNode;
}

/**
 * One kind's message drawn as it looks on the member's phone (task 10.15), beside its settings, so
 * the Owner knows exactly what is sent before turning it on.
 */
export function SmsPreview({ text, caption }: SmsPreviewProps) {
  const titleId = useId();

  return (
    // Named by its caption for screen readers, which do not all take a figcaption as the name.
    <figure aria-labelledby={titleId} className="space-y-2">
      <figcaption id={titleId} className="text-xs font-medium text-muted-foreground">
        پیش‌نمایش پیامک
      </figcaption>
      <div className="rounded-2xl rounded-ss-sm bg-muted px-4 py-3 text-sm leading-7 whitespace-pre-line">
        {text ?? <span className="text-muted-foreground">…</span>}
      </div>
      <p className="text-xs text-muted-foreground">{caption}</p>
    </figure>
  );
}
