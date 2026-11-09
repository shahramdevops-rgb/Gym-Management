import { fireEvent, screen } from "@testing-library/react";

import { json, mockApi } from "@/test/mockApi";

import { emptyHome, renderSite, siteAccess } from "../test/renderSite";
import { returnToStore } from "./returnTo";

/** A code answer whose «ارسال دوباره» opens two minutes from now. */
function codeAnswer() {
  const now = Date.now();
  return json(200, {
    outcome: "Sent",
    expiresAt: new Date(now + 60 * 60_000).toISOString(),
    sendAgainAt: new Date(now + 120_000).toISOString(),
  });
}

const signedIn = () =>
  json(200, {
    outcome: "SignedIn",
    access: siteAccess(),
    signUpTicket: null,
    signUpTicketExpiresAt: null,
  });

async function signInWithCode() {
  fireEvent.change(screen.getByLabelText("شماره موبایل"), { target: { value: "09121234567" } });
  fireEvent.click(screen.getByRole("button", { name: "دریافت کد" }));
  fireEvent.change(await screen.findByLabelText("کد پنج‌رقمی"), { target: { value: "12345" } });
}

describe("the page a signed-out person was opening (15.4d)", () => {
  it("SignIn_FromTheTrainersPage_OpensItAfterSigningIn", async () => {
    mockApi({
      ...emptyHome,
      "POST /api/site/auth/code": () => codeAnswer(),
      "POST /api/site/auth/sign-in": () => signedIn(),
      "GET /api/site/me": () => json(200, siteAccess().me),
      "GET /api/site/coaching/trainers": () => json(200, []),
    });
    renderSite("/trainers");

    expect(screen.getByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
    await signInWithCode();

    expect(await screen.findByRole("heading", { name: "انتخاب مربی" })).toBeInTheDocument();
    expect(returnToStore.target()).toBe("/");
  });

  it("SignIn_FromTheSignInScreen_OpensTheHome", async () => {
    mockApi({
      ...emptyHome,
      "POST /api/site/auth/code": () => codeAnswer(),
      "POST /api/site/auth/sign-in": () => signedIn(),
      "GET /api/site/me": () => json(200, siteAccess().me),
    });
    renderSite("/sign-in");

    await signInWithCode();

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
  });

  it("Remember_AnotherHostOrTheSignInScreen_IsNotKept", () => {
    returnToStore.remember("//example.com/trainers");
    expect(returnToStore.target()).toBe("/");

    returnToStore.remember("/sign-up");
    expect(returnToStore.target()).toBe("/");

    returnToStore.remember("/trainers");
    expect(returnToStore.target()).toBe("/trainers");
  });

  it("Target_StorageRefused_IsTheHome", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });

    expect(returnToStore.target()).toBe("/");
  });
});
