import { normalizePersianText } from "@/lib/normalize";

/** Nothing is searched for below this many characters, the same floor as member search (§2). */
export const nameSearchMinimumLength = 2;

/**
 * What the desk typed into the map's search field, ready to match names with, or `null` while it
 * is too short to search (BUSINESS_RULES.md §6 *Finding a member on the map*). It goes through the
 * same normalization as member search (§13), so «علي» finds «علی» and a half-space is a space.
 */
export function nameSearchTerm(text: string): string | null {
  const term = normalizePersianText(text).toLowerCase();
  return term.length >= nameSearchMinimumLength ? term : null;
}

/** Whether a name contains the term, anywhere in it. */
export function nameMatches(fullName: string, term: string): boolean {
  return normalizePersianText(fullName).toLowerCase().includes(term);
}
