import { fileURLToPath, URL } from "node:url";

import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// The API's HTTP endpoint from src/Gym.Api/Properties/launchSettings.json. HTTP, not HTTPS,
// so the dev server does not have to trust the local development certificate.
const apiOrigin = "http://localhost:5134";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    port: 5173,
    // The browser talks to one origin in development, exactly as it will in production where
    // Caddy serves the build and proxies /api. CORS stays a development-only safety net
    // instead of something the application depends on, and cookies behave the same in both.
    proxy: {
      "/api": { target: apiOrigin, changeOrigin: true },
      // /health is mapped at the API root rather than under /api, so it needs its own rule.
      "/health": { target: apiOrigin, changeOrigin: true },
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
  },
});
