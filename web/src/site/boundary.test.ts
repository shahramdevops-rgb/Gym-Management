/**
 * The site app and the panel share `lib/` and `components/` and nothing else (ADR 0010): the site
 * never ships a panel page, and above all never the panel's sign-in (its session, its fetch, its
 * client), which would put the staff's token handling in a bundle any member downloads.
 */
// Tests are not shipped, and one of them signs in to the panel on purpose (siteAuthFetch.test.ts).
const siteFiles = import.meta.glob<string>(["./**/*.{ts,tsx}", "!./**/*.test.{ts,tsx}"], {
  query: "?raw",
  import: "default",
  eager: true,
});

// What the site shares with the panel. The panel's own client and fetch live in lib/api and are
// the two files left out; anything else here that imported them would carry them into the site.
const sharedFiles = import.meta.glob<string>(
  [
    "../lib/**/*.{ts,tsx}",
    "../components/**/*.{ts,tsx}",
    "!../lib/api/client.ts",
    "!../lib/api/authFetch.ts",
    "!../**/*.test.{ts,tsx}",
  ],
  { query: "?raw", import: "default", eager: true },
);

const panelFiles = import.meta.glob<string>(["../**/*.{ts,tsx}", "!./**", "!../test/**"], {
  query: "?raw",
  import: "default",
  eager: true,
});

/** Every module a file imports, as written: `import … from "x"` and `import("x")`. */
function importsOf(source: string): string[] {
  return [...source.matchAll(/(?:from\s+|import\s*\(\s*)"([^"]+)"/g)].map((match) => match[1]!);
}

const panelOnly = [
  /^@\/app\//,
  /^@\/features\//,
  /^@\/lib\/api\/client$/,
  /^@\/lib\/api\/authFetch$/,
];

/** Inside lib/api the panel's client and fetch are reached by their relative names. */
const panelOnlyFromShared = [...panelOnly, /^\.\/(client|authFetch)$/];

function panelImportsIn(files: Record<string, string>, patterns = panelOnly): string[] {
  return Object.entries(files).flatMap(([file, source]) =>
    importsOf(source)
      .filter((module) => patterns.some((pattern) => pattern.test(module)))
      .map((module) => `${file} imports ${module}`),
  );
}

describe("the site app's boundary", () => {
  it("SiteFiles_Imports_NeverReachThePanel", () => {
    expect(Object.keys(siteFiles).length).toBeGreaterThan(10);
    expect(panelImportsIn(siteFiles)).toEqual([]);
  });

  it("SharedFiles_Imports_NeverCarryThePanelIntoTheSite", () => {
    expect(Object.keys(sharedFiles).length).toBeGreaterThan(20);
    expect(panelImportsIn(sharedFiles, panelOnlyFromShared)).toEqual([]);
  });

  it("PanelFiles_Imports_NeverReachTheSite", () => {
    const offending = Object.entries(panelFiles).flatMap(([file, source]) =>
      importsOf(source)
        .filter((module) => module.startsWith("@/site/"))
        .map((module) => `${file} imports ${module}`),
    );

    expect(Object.keys(panelFiles).length).toBeGreaterThan(100);
    expect(offending).toEqual([]);
  });
});
