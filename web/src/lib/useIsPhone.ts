import { useSyncExternalStore } from "react";

/**
 * Below Tailwind's `lg` (1024px): a phone, or a window as narrow as one (13.1a). The same line the
 * CSS draws with `max-lg:`, where the side menu becomes a drawer. The desk's computer and the backup
 * tablet are wider.
 */
const phoneQuery = "(max-width: 63.99rem)";

function subscribe(onChange: () => void) {
  const query = window.matchMedia(phoneQuery);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

function isPhoneNow() {
  return window.matchMedia(phoneQuery).matches;
}

/**
 * Whether the screen is a phone's, kept up to date as the window turns or is resized. For what CSS
 * alone cannot do, such as putting a part of the page somewhere else; anything that is only a
 * matter of size or of showing and hiding stays in `max-lg:` classes.
 */
export function useIsPhone(): boolean {
  return useSyncExternalStore(subscribe, isPhoneNow);
}
