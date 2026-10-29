import { fireEvent, render, screen } from "@testing-library/react";
import { vi } from "vitest";

import { pretendPhone } from "@/test/phone";

import { InstallHint } from "./InstallHint";
import { installPromptStore, type BeforeInstallPromptEvent } from "./installPrompt";

const iPhoneAgent =
  "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";

const androidAgent =
  "Mozilla/5.0 (Linux; Android 14; SM-A546E) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36";

const windowsAgent =
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

/** The browser's offer to install, on a phone of this kind (Android unless said). */
function browserOffer(agent = androidAgent) {
  vi.spyOn(navigator, "userAgent", "get").mockReturnValue(agent);
  const prompt = vi.fn(() => Promise.resolve());
  const offer = Object.assign(new Event("beforeinstallprompt"), {
    prompt,
    userChoice: Promise.resolve({ outcome: "accepted" as const }),
  }) as BeforeInstallPromptEvent;
  installPromptStore.set(offer);
  return prompt;
}

describe("InstallHint", () => {
  afterEach(() => installPromptStore.set(null));

  it("InstallHint_AndroidWithTheBrowsersOffer_InstallsOnPress", () => {
    const prompt = browserOffer();

    render(<InstallHint />);
    fireEvent.click(screen.getByRole("button", { name: "نصب برنامه" }));

    expect(prompt).toHaveBeenCalledOnce();
    // The offer is used once: the card goes with it.
    expect(screen.queryByRole("heading", { name: "نصب برنامه روی گوشی" })).not.toBeInTheDocument();
  });

  it("InstallHint_ComputerWithTheBrowsersOffer_ShowsNothing", () => {
    // Chrome and Edge offer to install on Windows too; a computer has no need of the app.
    browserOffer(windowsAgent);

    render(<InstallHint />);

    expect(screen.queryByRole("heading", { name: "نصب برنامه روی گوشی" })).not.toBeInTheDocument();
  });

  it("InstallHint_NoOfferAndNotAnIPhone_ShowsNothing", () => {
    vi.spyOn(navigator, "userAgent", "get").mockReturnValue(androidAgent);

    render(<InstallHint />);

    expect(screen.queryByRole("heading", { name: "نصب برنامه روی گوشی" })).not.toBeInTheDocument();
  });

  it("InstallHint_IPhone_ShowsSafarisSteps", () => {
    vi.spyOn(navigator, "userAgent", "get").mockReturnValue(iPhoneAgent);

    render(<InstallHint />);

    expect(screen.getByRole("heading", { name: "نصب برنامه روی گوشی" })).toBeInTheDocument();
    expect(screen.getByText("«Add to Home Screen»")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "نصب برنامه" })).not.toBeInTheDocument();
  });

  it("InstallHint_OpenedFromTheIcon_ShowsNothing", () => {
    browserOffer();
    // Every media query matches, display-mode: standalone among them.
    pretendPhone();

    render(<InstallHint />);

    expect(screen.queryByRole("heading", { name: "نصب برنامه روی گوشی" })).not.toBeInTheDocument();
  });

  it("InstallHint_Later_HidesItAndIsRemembered", () => {
    browserOffer();

    const { unmount } = render(<InstallHint />);
    fireEvent.click(screen.getByRole("button", { name: "بعداً" }));
    expect(screen.queryByRole("heading", { name: "نصب برنامه روی گوشی" })).not.toBeInTheDocument();

    unmount();
    render(<InstallHint />);
    expect(screen.queryByRole("heading", { name: "نصب برنامه روی گوشی" })).not.toBeInTheDocument();
  });

  it("InstallHint_LaterOverThirtyDaysAgo_ShowsAgain", () => {
    browserOffer();
    localStorage.setItem(
      "gym-site-install-hint-dismissed-at",
      String(Date.now() - 31 * 24 * 60 * 60 * 1000),
    );

    render(<InstallHint />);

    expect(screen.getByRole("heading", { name: "نصب برنامه روی گوشی" })).toBeInTheDocument();
  });
});
