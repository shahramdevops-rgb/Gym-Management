import { useEffect, useMemo, useRef } from "react";

/**
 * Runs `callback` only after calls have stopped for `delayMs`: each call restarts the timer.
 *
 * The search box uses it so typing "رضا احمدی" sends one request when the user pauses, not
 * one per key press. It debounces the call rather than a value, so the caller acts in an event
 * handler instead of an effect that watches a value (which would run again whenever anything
 * it depends on changes, not only when the user typed).
 *
 * `cancel` drops a pending call, for example when Enter searches at once.
 */
export function useDebouncedCallback<TArgs extends unknown[]>(
  callback: (...args: TArgs) => void,
  delayMs: number,
) {
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const latest = useRef(callback);

  // The newest callback, so a call scheduled before a re-render still uses current values.
  useEffect(() => {
    latest.current = callback;
  });

  // A pending call must not fire after the page is gone.
  useEffect(() => () => clearTimeout(timer.current), []);

  return useMemo(
    () => ({
      run: (...args: TArgs) => {
        clearTimeout(timer.current);
        timer.current = setTimeout(() => latest.current(...args), delayMs);
      },
      cancel: () => clearTimeout(timer.current),
    }),
    [delayMs],
  );
}
