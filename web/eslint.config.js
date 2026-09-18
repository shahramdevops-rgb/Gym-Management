import js from "@eslint/js";
import prettier from "eslint-config-prettier";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config([
  { ignores: ["dist", "coverage", "src/lib/api/schema.d.ts"] },
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
      // Last, so formatting rules that would fight Prettier are switched off.
      prettier,
    ],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    rules: {
      // The UI is right-to-left. A physical utility class silently mirrors the layout, and it
      // is invisible in review because the class name looks perfectly normal. The build says no.
      "no-restricted-syntax": [
        "error",
        {
          selector:
            "Literal[value=/(^|\s)-?(ml|mr|pl|pr|border-l|border-r|rounded-l|rounded-r|left|right|text-left|text-right)-/]",
          message:
            "Use logical Tailwind utilities (ms-, me-, ps-, pe-, start-, end-, text-start, text-end) instead of physical ones.",
        },
      ],
    },
  },
]);
