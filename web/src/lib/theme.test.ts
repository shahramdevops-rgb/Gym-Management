import { applyTheme, readSavedTheme, themeStorageKey } from "./theme";

describe("theme", () => {
  it("readSavedTheme_NothingSaved_IsLight", () => {
    expect(readSavedTheme()).toBe("light");
  });

  it("readSavedTheme_DarkSaved_IsDark", () => {
    localStorage.setItem(themeStorageKey, "dark");

    expect(readSavedTheme()).toBe("dark");
  });

  it("readSavedTheme_SomethingElseSaved_IsLight", () => {
    localStorage.setItem(themeStorageKey, "purple");

    expect(readSavedTheme()).toBe("light");
  });

  it("readSavedTheme_StorageBlocked_IsLight", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("SecurityError");
    });

    expect(readSavedTheme()).toBe("light");
  });

  it("applyTheme_Dark_PutsTheClassOnHtmlAndSavesIt", () => {
    applyTheme("dark");

    expect(document.documentElement).toHaveClass("dark");
    expect(localStorage.getItem(themeStorageKey)).toBe("dark");
  });

  it("applyTheme_LightAfterDark_TakesTheClassOffAndSavesIt", () => {
    applyTheme("dark");
    applyTheme("light");

    expect(document.documentElement).not.toHaveClass("dark");
    expect(localStorage.getItem(themeStorageKey)).toBe("light");
  });

  it("applyTheme_StorageBlocked_StillSwitchesTheScreen", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("QuotaExceededError");
    });

    applyTheme("dark");

    expect(document.documentElement).toHaveClass("dark");
  });
});
