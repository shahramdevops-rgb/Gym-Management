import { vi } from "vitest";

/**
 * Makes the test's screen a phone's for `useIsPhone` (13.1a): every media query matches. jsdom
 * applies no CSS, so this changes only what the code places differently on a phone, never a
 * `max-lg:` class. Undone after the test with the other mocks.
 */
export function pretendPhone() {
  vi.spyOn(window, "matchMedia").mockImplementation(
    (query: string) =>
      ({
        matches: true,
        media: query,
        onchange: null,
        addEventListener() {},
        removeEventListener() {},
        addListener() {},
        removeListener() {},
        dispatchEvent: () => false,
      }) as MediaQueryList,
  );
}
