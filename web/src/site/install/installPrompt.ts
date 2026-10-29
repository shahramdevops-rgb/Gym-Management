/**
 * Chrome's and Samsung Internet's offer to install the app (roadmap 15.3b). The browser announces
 * it once, early, with `beforeinstallprompt`, often before React has drawn anything, so it is
 * caught here from main.tsx and kept until «نصب برنامه» is pressed. An iPhone never announces it:
 * Safari installs only from its Share menu, which `InstallHint` explains instead.
 */

/** Not in TypeScript's DOM types: Chromium's own event. */
export interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

let offer: BeforeInstallPromptEvent | null = null;
const listeners = new Set<() => void>();

function set(next: BeforeInstallPromptEvent | null) {
  offer = next;
  listeners.forEach((listener) => listener());
}

export const installPromptStore = {
  get: (): BeforeInstallPromptEvent | null => offer,
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  /** Shows the browser's own install question; the offer can be used once. */
  async install(): Promise<void> {
    const current = offer;
    if (current === null) {
      return;
    }
    set(null);
    await current.prompt();
  },
  set,
};

export function captureInstallPrompt(): void {
  window.addEventListener("beforeinstallprompt", (event) => {
    // The app shows its own card at the right moment instead of the browser's bar at the bottom.
    event.preventDefault();
    set(event as BeforeInstallPromptEvent);
  });
  window.addEventListener("appinstalled", () => set(null));
}

/** Opened from the home-screen icon rather than in a browser tab. */
export function isInstalledApp(): boolean {
  try {
    if (window.matchMedia("(display-mode: standalone)").matches) {
      return true;
    }
  } catch {
    // No media queries: a very old browser, in a tab.
  }
  // Safari's own flag, from before it supported display-mode.
  return (navigator as Navigator & { standalone?: boolean }).standalone === true;
}

/** An iPhone or an iPad, including an iPad that says it is a Mac. */
export function isAppleMobile(): boolean {
  const agent = navigator.userAgent;
  return (
    /iPhone|iPad|iPod/.test(agent) || (agent.includes("Macintosh") && navigator.maxTouchPoints > 1)
  );
}

/**
 * A phone or a tablet, where the app is meant to be installed. Chrome and Edge offer to install on
 * a computer too, but nobody there needs the app (the developer, 15.3b): the card stays away.
 */
export function isMobileDevice(): boolean {
  return isAppleMobile() || /Android/.test(navigator.userAgent);
}
