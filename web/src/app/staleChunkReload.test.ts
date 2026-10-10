import { describe, expect, it, vi } from "vitest";

import { installStaleChunkReload, staleChunkReloadGapMs } from "./staleChunkReload";

function setup(start = 1_000_000) {
  const target = new EventTarget();
  const values = new Map<string, string>();
  const storage = {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => void values.set(key, value),
  };
  const reload = vi.fn();
  let now = start;

  installStaleChunkReload({ target, storage, reload, now: () => now });

  const fail = () => {
    const event = new Event("vite:preloadError", { cancelable: true });
    target.dispatchEvent(event);
    return event;
  };

  return { reload, fail, advance: (ms: number) => (now += ms) };
}

describe("installStaleChunkReload", () => {
  it("reloads the page when a page file fails to load after a release", () => {
    const { reload, fail } = setup();

    const event = fail();

    expect(reload).toHaveBeenCalledTimes(1);
    expect(event.defaultPrevented).toBe(true);
  });

  it("lets a second failure soon after the reload reach the error screen", () => {
    const { reload, fail, advance } = setup();
    fail();
    advance(staleChunkReloadGapMs - 1);

    const event = fail();

    expect(reload).toHaveBeenCalledTimes(1);
    expect(event.defaultPrevented).toBe(false);
  });

  it("reloads again once the gap has passed", () => {
    const { reload, fail, advance } = setup();
    fail();
    advance(staleChunkReloadGapMs);

    fail();

    expect(reload).toHaveBeenCalledTimes(2);
  });

  it("still reloads when storage is blocked", () => {
    const target = new EventTarget();
    const reload = vi.fn();
    const blocked = {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
    };
    installStaleChunkReload({ target, storage: blocked, reload, now: () => 1 });

    target.dispatchEvent(new Event("vite:preloadError", { cancelable: true }));

    expect(reload).toHaveBeenCalledTimes(1);
  });
});
