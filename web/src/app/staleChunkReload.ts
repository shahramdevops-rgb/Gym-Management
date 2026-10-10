const reloadedAtKey = "gym.staleChunkReloadAt";

/** Within this long of the last reload, a failed page file is a real fault, not a release. */
export const staleChunkReloadGapMs = 10_000;

interface StaleChunkReloadOptions {
  target?: EventTarget;
  storage?: Pick<Storage, "getItem" | "setItem">;
  reload?: () => void;
  now?: () => number;
}

/**
 * Reloads the page when a page's file fails to download (task 11.3).
 *
 * Every page but the board is downloaded the first time it is opened, and a release replaces the
 * files on the server with new hashed names. A tab opened before the release, as the desk's is
 * all day, then asks for a file that no longer exists. Vite reports that as `vite:preloadError`;
 * reloading fetches the new version and the page opens. At most once per
 * {@link staleChunkReloadGapMs}, so a file that is missing for another reason reaches the page's
 * error screen instead of reloading for ever.
 */
export function installStaleChunkReload({
  target = window,
  storage = window.sessionStorage,
  reload = () => window.location.reload(),
  now = Date.now,
}: StaleChunkReloadOptions = {}) {
  target.addEventListener("vite:preloadError", (event) => {
    const last = read(storage);
    if (last !== null && now() - Number(last) < staleChunkReloadGapMs) {
      return;
    }

    event.preventDefault();
    write(storage, String(now()));
    reload();
  });
}

// Storage can be blocked (a private window, site data turned off); the reload still happens,
// only without the guard.
function read(storage: StaleChunkReloadOptions["storage"]) {
  try {
    return storage?.getItem(reloadedAtKey) ?? null;
  } catch {
    return null;
  }
}

function write(storage: StaleChunkReloadOptions["storage"], value: string) {
  try {
    storage?.setItem(reloadedAtKey, value);
  } catch {
    // See read().
  }
}
