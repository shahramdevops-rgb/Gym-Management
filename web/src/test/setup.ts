// Adds DOM matchers such as toBeInTheDocument() to Vitest's expect.
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

import { sessionStore } from "@/features/auth/session";

// Each test renders into a fresh document and starts with the real globals again. The session
// lives in a module, not in React, so it is reset by hand: otherwise one test's login would
// leak into the next.
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  sessionStore.reset();
});
