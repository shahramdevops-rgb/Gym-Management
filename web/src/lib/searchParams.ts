/**
 * The page number from a URL like `?page=3`. Anything missing, not a whole number, or below 1
 * reads as page 1, so a hand-edited or stale link still opens a working list.
 */
export function pageFromParams(params: URLSearchParams): number {
  const page = Number(params.get("page"));

  return Number.isInteger(page) && page >= 1 ? page : 1;
}

const isoDate = /^\d{4}-\d{2}-\d{2}$/;

/**
 * An ISO date from the URL (`?from=2026-09-01`), or undefined for one that is missing or
 * hand-edited into nonsense — the list then opens unbounded on that side instead of failing.
 */
export function dateFromParams(params: URLSearchParams, name: string): string | undefined {
  const value = params.get(name) ?? "";

  return isoDate.test(value) ? value : undefined;
}
