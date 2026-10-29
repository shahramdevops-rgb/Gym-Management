// Adds DOM matchers such as toBeInTheDocument() to Vitest's expect.
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

import { sessionStore } from "@/features/auth/session";
import { siteSessionStore } from "@/site/auth/session";
import { signUpTicketStore } from "@/site/auth/signUpTicket";

// jsdom measures nothing and has no ResizeObserver, which the dashboard's charts (Recharts,
// `responsive`) listen with. Set on the global directly rather than with vi.stubGlobal, which the
// hook below would undo after the first test. A chart then draws at no size: the tests read the
// figures around it, not its pixels.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

// jsdom has no media queries either; `useIsPhone` asks one (13.1a). A query that never matches and
// never changes: the tests render the computer's layout unless a test stubs a phone.
window.matchMedia ??= (query: string) =>
  ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener() {},
    removeEventListener() {},
    addListener() {},
    removeListener() {},
    dispatchEvent: () => false,
  }) as MediaQueryList;

// Each test renders into a fresh document and starts with the real globals again. The session
// lives in a module, not in React, so it is reset by hand: otherwise one test's login would
// leak into the next. The theme lives on <html> and in localStorage, which outlive a render.
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  sessionStore.reset();
  siteSessionStore.reset();
  signUpTicketStore.clear();
  document.documentElement.classList.remove("dark");
  localStorage.clear();
});
