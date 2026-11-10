import { sitePaths } from "../paths";

/**
 * Where a signed-out person was going (roadmap 15.4d): the public page's «انتخاب این مربی» opens
 * `/trainers`, which sends them to sign in first; once signed in or signed up, they are taken there
 * instead of the home.
 *
 * Kept for the tab in `sessionStorage`, not in memory: a phone may reload the page while the person
 * reads the code in their messages. Storage can be refused (a private window, blocked site data);
 * the person then lands on the home, as before 15.4d.
 */
const key = "gym-site-return-to";

/** Only one of the app's own pages: never another host (`//…`), never the sign-in screens themselves. */
function isOwnPage(path: string): boolean {
  return (
    path.startsWith("/") &&
    !path.startsWith("//") &&
    !path.startsWith("/\\") &&
    path !== sitePaths.home &&
    !path.startsWith(sitePaths.signIn) &&
    !path.startsWith(sitePaths.signUp)
  );
}

export const returnToStore = {
  /** Called when a signed-out person opens a page that needs a session. */
  remember: (path: string) => {
    if (!isOwnPage(path)) {
      return;
    }
    try {
      sessionStorage.setItem(key, path);
    } catch {
      // No storage: the home after signing in.
    }
  },

  /** Where to go once signed in: the page remembered, or the home. Read again on each render. */
  target: (): string => {
    try {
      const path = sessionStorage.getItem(key);
      return path !== null && isOwnPage(path) ? path : sitePaths.home;
    } catch {
      return sitePaths.home;
    }
  },

  /** Called once a page that needs a session is shown: the way there is over. */
  clear: () => {
    try {
      sessionStorage.removeItem(key);
    } catch {
      // Nothing was kept.
    }
  },
};
