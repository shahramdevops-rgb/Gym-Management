/**
 * The page number from a URL like `?page=3`. Anything missing, not a whole number, or below 1
 * reads as page 1, so a hand-edited or stale link still opens a working list.
 */
export function pageFromParams(params: URLSearchParams): number {
  const page = Number(params.get("page"));

  return Number.isInteger(page) && page >= 1 ? page : 1;
}
