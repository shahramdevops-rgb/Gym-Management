/** The site app's addresses (roadmap 15.3). Few, and none shared with the panel's. */
export const sitePaths = {
  home: "/",
  signIn: "/sign-in",
  signUp: "/sign-up",
  /** The member picks a trainer (15.4). */
  trainers: "/trainers",
} as const;

/**
 * The gym's public page, linked at the foot of the home (asked by the developer, 2026-11-03).
 * Its own host on the server, the address its canonical link names; in development the copy
 * `npm run dev:site` serves beside the app (web/vite.site.config.ts).
 */
export const gymSiteAddress = import.meta.env.PROD
  ? "https://pasargadgymplus.ir/"
  : "/public-site/";
