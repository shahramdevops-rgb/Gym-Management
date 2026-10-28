import { fileURLToPath, URL } from "node:url";

import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// The site app for members and trainers (roadmap 15.3, ADR 0010): a second entry in web/, built
// on its own into dist-site/ and served on its own host (app.pasargadgymplus.ir). Its page is
// site/index.html; its code is src/site/, which shares src/lib and src/components with the panel
// and none of the panel's pages (src/site/boundary.test.ts). Its tests run with the panel's, from
// vite.config.ts.
//
//   npm run dev:site      http://localhost:5174
//   npm run build         builds the panel, then this
const apiOrigin = "http://localhost:5134";

export default defineConfig({
  root: fileURLToPath(new URL("./site", import.meta.url)),
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  build: {
    outDir: fileURLToPath(new URL("./dist-site", import.meta.url)),
    emptyOutDir: true,
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
