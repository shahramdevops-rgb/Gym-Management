// Adds DOM matchers such as toBeInTheDocument() to Vitest's expect.
import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// Each test renders into a fresh document and starts with the real globals again.
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});
