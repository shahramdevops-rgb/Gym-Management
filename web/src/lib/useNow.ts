import { useEffect, useState } from "react";

/**
 * The current moment, refreshed every `intervalMs`: the component re-renders on each tick, so
 * anything drawn from how long ago something happened moves on by itself without a new request.
 *
 * Call it once, as high as the drawing needs, and pass `now` down: one timer for a whole screen
 * rather than one per door.
 */
export function useNow(intervalMs: number): Date {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);

  return now;
}
