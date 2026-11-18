import { createHash } from "node:crypto";
import { readFileSync, statSync } from "node:fs";
import { extname, join, normalize, sep } from "node:path";
import { fileURLToPath, URL } from "node:url";

import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, type Plugin } from "vite";

// The site app for members and trainers (roadmap 15.3, ADR 0010): a second entry in web/, built
// on its own into dist-site/ and served on its own host (app.pasargadgymplus.ir). Its page is
// site/index.html; its code is src/site/, which shares src/lib and src/components with the panel
// and none of the panel's pages (src/site/boundary.test.ts). Its tests run with the panel's, from
// vite.config.ts.
//
// The same build holds a second page, the shop's showcase (roadmap 16.4): site/shop/index.html,
// code in src/site/shop/, served on the gym's public host under /shop/.
//
//   npm run dev:site      http://localhost:5174          the app
//                         http://localhost:5174/shop/    the shop's showcase
//                         http://localhost:5174/public-site/   the gym's public page
//   npm run build         builds the panel, then this
const apiOrigin = "http://localhost:5134";

const serviceWorkerTemplate = fileURLToPath(new URL("./site/sw.js", import.meta.url));

/**
 * Emits dist-site/sw.js from site/sw.js with this build's files in it (roadmap 15.3b). Their names
 * carry their content's hash, so the list itself is the version: a release that changes any file
 * gives a different sw.js, and a phone that sees it installs the new version by itself. Written
 * here rather than with a PWA package: it is one list and one hash.
 */
function siteServiceWorker(): Plugin {
  return {
    name: "gym-site-service-worker",
    apply: "build",
    // After Vite's own plugins, so the list is the bundle as it is written, index.html included.
    generateBundle: {
      order: "post",
      handler(_options, bundle) {
        const assets = Object.keys(bundle)
          .filter((fileName) => fileName.startsWith("assets/"))
          .sort()
          .map((fileName) => `/${fileName}`);
        const page = bundle["index.html"];
        if (page === undefined || page.type !== "asset") {
          throw new Error("gym-site-service-worker: the build has no index.html");
        }

        const template = readFileSync(serviceWorkerTemplate, "utf8");
        // The page is kept for offline too, so a release that changes only the page is a new
        // version as well.
        const version = createHash("sha256")
          .update(template)
          .update(page.source)
          .update(readFileSync(new URL("./site/public/theme-init.js", import.meta.url)))
          .update(assets.join("\n"))
          .digest("hex")
          .slice(0, 12);

        const source = template
          .replace('"__VERSION__"', JSON.stringify(version))
          .replace('["__ASSETS__"]', JSON.stringify(assets));
        // A template reworded or reformatted would otherwise ship its placeholders: the worker
        // would fail to install, and nothing would say so.
        if (source.includes("__VERSION__") || source.includes("__ASSETS__")) {
          throw new Error(`gym-site-service-worker: placeholders left in ${serviceWorkerTemplate}`);
        }

        this.emitFile({ type: "asset", fileName: "sw.js", source });
      },
    },
  };
}

const publicSiteRoot = fileURLToPath(new URL("./public-site", import.meta.url));
const publicSitePrefix = "/public-site/";

const contentTypes: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".xml": "application/xml; charset=utf-8",
  ".txt": "text/plain; charset=utf-8",
  ".webp": "image/webp",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".woff2": "font/woff2",
};

/**
 * In development only, the gym's public page (web/public-site) at
 * http://localhost:5174/public-site/, beside the app (15.3b). Its «ورود / ثبت‌نام» goes to /app,
 * which is this app's own address here (its router sends it to the sign-in screen) and Caddy's
 * redirect to the app's host on the server: the whole way from the gym's page to signing in, on
 * one address and with the real code SMS of development (BUSINESS_RULES.md §10).
 */
function publicSitePreview(): Plugin {
  return {
    name: "gym-public-site-preview",
    apply: "serve",
    configureServer(server) {
      server.middlewares.use((request, response, next) => {
        const path = (request.url ?? "").split("?")[0];
        // The trainer card's «انتخاب این مربی» (15.4d), as Caddy's redirect on the server.
        if (path === "/app/trainers") {
          response.writeHead(302, { Location: "/trainers" }).end();
          return;
        }
        if (path === publicSitePrefix.slice(0, -1)) {
          response.writeHead(302, { Location: publicSitePrefix }).end();
          return;
        }
        if (!path.startsWith(publicSitePrefix)) {
          next();
          return;
        }

        // A folder's address is its index.html (the pages of their own, contact/ and privacy/,
        // roadmap 16.9), as Caddy's file_server answers on the server.
        const relative = decodeURIComponent(path.slice(publicSitePrefix.length));
        const file = normalize(
          join(
            publicSiteRoot,
            relative === "" || relative.endsWith("/") ? `${relative}index.html` : relative,
          ),
        );
        // Nothing outside the folder, whatever the address says.
        if (!file.startsWith(publicSiteRoot + sep)) {
          response.writeHead(404).end();
          return;
        }
        try {
          if (!statSync(file).isFile()) {
            throw new Error("not a file");
          }
        } catch {
          response.writeHead(404).end();
          return;
        }

        response.writeHead(200, {
          "Content-Type": contentTypes[extname(file)] ?? "application/octet-stream",
          "Cache-Control": "no-cache",
        });
        response.end(readFileSync(file));
      });
    },
  };
}

const shopPrefix = "/shop/";

/**
 * In development only, every address of the shop (/shop/product/…) answers with the shop's page
 * (site/shop/index.html), whose router reads the address; on the server Caddy does the same for
 * the gym's public host. Without this, Vite's own fallback would answer with the app's page.
 */
function shopPageFallback(): Plugin {
  return {
    name: "gym-shop-page-fallback",
    apply: "serve",
    configureServer(server) {
      server.middlewares.use((request, _response, next) => {
        const [path = "", query] = (request.url ?? "").split("?");
        // A page, not a file: the shop's own modules (/shop/main.ts) have an extension.
        if (path.startsWith(shopPrefix) && extname(path) === "") {
          request.url = `${shopPrefix}index.html${query === undefined ? "" : `?${query}`}`;
        }
        next();
      });
    },
  };
}

export default defineConfig({
  root: fileURLToPath(new URL("./site", import.meta.url)),
  plugins: [react(), tailwindcss(), siteServiceWorker(), publicSitePreview(), shopPageFallback()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  build: {
    outDir: fileURLToPath(new URL("./dist-site", import.meta.url)),
    emptyOutDir: true,
    // Two pages from one build (roadmap 16.4): the app (index.html, on its own host) and the
    // shop's showcase (shop/index.html, on the gym's public host under /shop/). They share the
    // hashed files in assets/, which Caddy serves on both hosts.
    rollupOptions: {
      input: {
        main: fileURLToPath(new URL("./site/index.html", import.meta.url)),
        shop: fileURLToPath(new URL("./site/shop/index.html", import.meta.url)),
      },
    },
  },
  server: {
    // Beside the panel's 5173, so both run at once. One origin with the API, as on the server:
    // the site's refresh cookie (path /api/site/auth) is then sent exactly as in production.
    port: 5174,
    strictPort: true,
    proxy: {
      "/api": { target: apiOrigin, changeOrigin: true },
    },
  },
});
