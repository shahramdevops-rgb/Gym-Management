import { Moon, Sun } from "lucide-react";

import { Button } from "@/components/ui/button";
import { useTheme } from "@/lib/theme";

/**
 * The switch between the light and the dark theme, in the header (BUSINESS_RULES.md §14). It is a
 * pressed / not-pressed button named «تم تیره», so a screen reader hears one name and its state;
 * the icon shows the theme it will switch to.
 */
export function ThemeToggle() {
  const { theme, toggle } = useTheme();
  const dark = theme === "dark";

  return (
    <Button
      variant="ghost"
      size="sm"
      aria-label="تم تیره"
      title={dark ? "تم روشن" : "تم تیره"}
      aria-pressed={dark}
      onClick={toggle}
    >
      {dark ? <Sun aria-hidden /> : <Moon aria-hidden />}
    </Button>
  );
}
