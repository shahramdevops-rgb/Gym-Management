import { fireEvent, screen, waitFor } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { siteAccess, renderSite } from "../../test/renderSite";

/** A code answer whose «ارسال دوباره» opens `secondsToResend` from now. */
function codeAnswer(outcome: string, secondsToResend = 120) {
  const now = Date.now();
  return json(200, {
    outcome,
    expiresAt: new Date(now + 60 * 60_000).toISOString(),
    sendAgainAt: new Date(now + secondsToResend * 1000).toISOString(),
  });
}

function askForCode(phone = "۰۹۱۲۱۲۳۴۵۶۷") {
  fireEvent.change(screen.getByLabelText("شماره موبایل"), { target: { value: phone } });
  fireEvent.click(screen.getByRole("button", { name: "دریافت کد" }));
}

function typeCode(code: string) {
  fireEvent.change(screen.getByLabelText("کد پنج‌رقمی"), { target: { value: code } });
}

describe("SignInPage", () => {
  it("SignIn_SignedOutVisitor_IsSentToTheSignInScreen", () => {
    mockApi({});

    renderSite("/");

    expect(screen.getByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
  });

  it("SignIn_EmptyPhone_ShowsPersianErrorWithoutCallingTheApi", async () => {
    const api = mockApi({});
    renderSite("/sign-in");

    fireEvent.click(screen.getByRole("button", { name: "دریافت کد" }));

    expect(await screen.findByText("شماره موبایل را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/code")).toHaveLength(0);
  });

  it("SignIn_PersianDigits_AreSentAsEnglishDigitsAndTheCodeStepOpens", async () => {
    const api = mockApi({ "POST /api/site/auth/code": () => codeAnswer("Sent") });
    renderSite("/sign-in");

    askForCode();

    expect(await screen.findByRole("heading", { name: "کد ورود" })).toBeInTheDocument();
    expect(screen.getByText(/پیامک شد/)).toHaveTextContent("۰۹۱۲۱۲۳۴۵۶۷");
    expect(await api.requestsTo("POST", "/api/site/auth/code")[0]!.json()).toEqual({
      phoneNumber: "09121234567",
    });
  });

  it("SignIn_CodeBox_IsOfferedTheCodeFromTheSms", async () => {
    mockApi({ "POST /api/site/auth/code": () => codeAnswer("Sent") });
    renderSite("/sign-in");

    askForCode();

    const box = await screen.findByLabelText("کد پنج‌رقمی");
    expect(box).toHaveAttribute("autocomplete", "one-time-code");
    expect(box).toHaveAttribute("inputmode", "numeric");
  });

  it("SignIn_JustSent_WaitsBeforeSendAgain", async () => {
    mockApi({ "POST /api/site/auth/code": () => codeAnswer("Sent", 90) });
    renderSite("/sign-in");

    askForCode();

    const sendAgain = await screen.findByRole("button", { name: /^ارسال دوباره \(۱:[۲۳][۰-۹]\)$/ });
    expect(sendAgain).toBeDisabled();
  });

  it("SignIn_AlreadyDelivered_SaysTheCodeIsOnThePhone", async () => {
    mockApi({ "POST /api/site/auth/code": () => codeAnswer("AlreadyDelivered", 3000) });
    renderSite("/sign-in");

    askForCode();

    expect(
      await screen.findByText("کد به گوشی شما رسیده است؛ پیامک‌هایتان را ببینید."),
    ).toBeInTheDocument();
  });

  it("SignIn_SendAgainWhenAllowed_AsksTheApiAgainAndShowsItsAnswer", async () => {
    let calls = 0;
    const api = mockApi({
      "POST /api/site/auth/code": () => codeAnswer(++calls === 1 ? "Sent" : "SentAgain", 0),
    });
    renderSite("/sign-in");

    askForCode();
    fireEvent.click(await screen.findByRole("button", { name: "ارسال دوباره" }));

    expect(await screen.findByText(/همان کد دوباره/)).toHaveTextContent("۶۰ دقیقهٔ دیگر");
    expect(api.requestsTo("POST", "/api/site/auth/code")).toHaveLength(2);
  });

  it("SignIn_GymLimitReached_ShowsThePersianMessage", async () => {
    mockApi({
      "POST /api/site/auth/code": () => problem(422, "SiteAuth.GymDailyLimitReached"),
    });
    renderSite("/sign-in");

    askForCode();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "امروز دیگر نمی‌شود کد فرستاد. فردا دوباره امتحان کنید یا به پذیرش باشگاه بگویید.",
    );
    expect(screen.queryByRole("heading", { name: "کد ورود" })).not.toBeInTheDocument();
  });

  it("SignIn_FifthDigit_SignsInWithoutAButtonAndOpensHome", async () => {
    const api = mockApi({
      "POST /api/site/auth/code": () => codeAnswer("Sent"),
      "POST /api/site/auth/sign-in": () =>
        json(200, {
          outcome: "SignedIn",
          access: siteAccess(),
          signUpTicket: null,
          signUpTicketExpiresAt: null,
        }),
      "GET /api/site/me": () => json(200, siteAccess().me),
    });
    renderSite("/sign-in");

    askForCode();
    await screen.findByLabelText("کد پنج‌رقمی");
    typeCode("۱۲۳۴۵");

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/sign-in")).toHaveLength(1);
    expect(await api.requestsTo("POST", "/api/site/auth/sign-in")[0]!.json()).toEqual({
      phoneNumber: "09121234567",
      code: "12345",
    });
  });

  it("SignIn_CodeFilledTwiceAtOnce_IsSentOnce", async () => {
    const api = mockApi({
      "POST /api/site/auth/code": () => codeAnswer("Sent"),
      "POST /api/site/auth/sign-in": () => problem(422, "SiteAuth.CodeWrong"),
    });
    renderSite("/sign-in");

    askForCode();
    const box = await screen.findByLabelText("کد پنج‌رقمی");
    fireEvent.change(box, { target: { value: "12345" } });
    fireEvent.change(box, { target: { value: "12345" } });

    expect(await screen.findByText("کد درست نیست.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/sign-in")).toHaveLength(1);
  });

  it("SignIn_ShortCodeSentByHand_NeverReachesTheApi", async () => {
    const api = mockApi({ "POST /api/site/auth/code": () => codeAnswer("Sent") });
    renderSite("/sign-in");

    askForCode();
    await screen.findByLabelText("کد پنج‌رقمی");
    typeCode("۱۲۳");
    fireEvent.click(screen.getByRole("button", { name: "ورود" }));

    expect(await screen.findByText("کد درست نیست.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/sign-in")).toHaveLength(0);
  });

  it("SignIn_AndroidReadsTheSms_SendsTheCodeByItself", async () => {
    // jsdom has neither. Without OTPCredential (unstubbed after each test) the app never reads
    // navigator.credentials, so the property left behind is harmless to the other tests.
    vi.stubGlobal("OTPCredential", class {});
    Object.defineProperty(navigator, "credentials", {
      configurable: true,
      value: { get: vi.fn().mockResolvedValue({ code: "54321" }) },
    });
    const api = mockApi({
      "POST /api/site/auth/code": () => codeAnswer("Sent"),
      "POST /api/site/auth/sign-in": () => problem(422, "SiteAuth.CodeWrong"),
    });
    renderSite("/sign-in");

    askForCode();

    expect(await screen.findByText("کد درست نیست.")).toBeInTheDocument();
    expect(await api.requestsTo("POST", "/api/site/auth/sign-in")[0]!.json()).toEqual({
      phoneNumber: "09121234567",
      code: "54321",
    });
  });

  it("SignIn_WrongCode_ShowsTheMessageUnderTheBox", async () => {
    mockApi({
      "POST /api/site/auth/code": () => codeAnswer("Sent"),
      "POST /api/site/auth/sign-in": () => problem(422, "SiteAuth.CodeWrong"),
    });
    renderSite("/sign-in");

    askForCode();
    await screen.findByLabelText("کد پنج‌رقمی");
    typeCode("11111");

    expect(await screen.findByText("کد درست نیست.")).toBeInTheDocument();
    expect(screen.getByLabelText("کد پنج‌رقمی")).toHaveAttribute("aria-invalid", "true");
  });

  it("SignIn_NewPhone_OpensSignUpWithTheProvedPhone", async () => {
    mockApi({
      "POST /api/site/auth/code": () => codeAnswer("Sent"),
      "POST /api/site/auth/sign-in": () =>
        json(200, {
          outcome: "NeedsProfile",
          access: null,
          signUpTicket: "ticket-1",
          signUpTicketExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        }),
    });
    renderSite("/sign-in");

    askForCode();
    await screen.findByLabelText("کد پنج‌رقمی");
    typeCode("12345");

    expect(await screen.findByRole("heading", { name: "ثبت‌نام" })).toBeInTheDocument();
    expect(screen.getByText(/هنوز در باشگاه ثبت نشده است/)).toHaveTextContent("۰۹۱۲۱۲۳۴۵۶۷");
  });

  it("SignIn_ChangePhone_GoesBackToThePhoneStep", async () => {
    mockApi({ "POST /api/site/auth/code": () => codeAnswer("Sent") });
    renderSite("/sign-in");

    askForCode();
    fireEvent.click(await screen.findByRole("button", { name: "تغییر شماره" }));

    await waitFor(() =>
      expect(screen.getByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument(),
    );
  });

  it("SignIn_AlreadySignedIn_GoesStraightHome", async () => {
    mockApi({ "GET /api/site/me": () => json(200, siteAccess().me) });

    renderSite("/sign-in", { access: siteAccess() });

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
  });
});
