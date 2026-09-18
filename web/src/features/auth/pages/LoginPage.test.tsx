import { fireEvent, screen } from "@testing-library/react";

import { sessionStore } from "@/features/auth/session";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

function fillAndSubmit(userName: string, password: string) {
  fireEvent.change(screen.getByLabelText("نام کاربری"), { target: { value: userName } });
  fireEvent.change(screen.getByLabelText("رمز عبور"), { target: { value: password } });
  fireEvent.click(screen.getByRole("button", { name: "ورود" }));
}

describe("LoginPage", () => {
  it("Login_SignedOutVisitor_IsSentToTheLoginPage", () => {
    mockApi({});

    renderApp("/staff");

    expect(screen.getByRole("heading", { name: "ورود به مدیریت باشگاه" })).toBeInTheDocument();
  });

  it("Login_EmptyForm_ShowsPersianErrorsWithoutCallingTheApi", async () => {
    const api = mockApi({});
    renderApp("/login");

    fireEvent.click(screen.getByRole("button", { name: "ورود" }));

    expect(await screen.findByText("نام کاربری را وارد کنید.")).toBeInTheDocument();
    expect(screen.getByText("رمز عبور را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/auth/login")).toHaveLength(0);
  });

  it("Login_WrongPassword_ShowsThePersianMessageForTheCode", async () => {
    mockApi({ "POST /api/auth/login": () => problem(401, "Auth.InvalidCredentials") });
    renderApp("/login");

    fillAndSubmit("sara", "Wrong1234");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "نام کاربری یا رمز عبور اشتباه است.",
    );
    expect(sessionStore.getState().status).toBe("signedOut");
  });

  it("Login_LockedOut_ShowsTheLockoutMessage", async () => {
    mockApi({ "POST /api/auth/login": () => problem(401, "Auth.LockedOut") });
    renderApp("/login");

    fillAndSubmit("sara", "Wrong1234");

    expect(await screen.findByRole("alert")).toHaveTextContent("موقتاً قفل شده است");
  });

  it("Login_TemporaryPassword_GoesToTheChangePasswordPage", async () => {
    mockApi({ "POST /api/auth/login": () => json(200, session({ mustChangePassword: true })) });
    renderApp("/login");

    fillAndSubmit("owner", "Temp1234");

    expect(await screen.findByRole("heading", { name: "تغییر رمز عبور" })).toBeInTheDocument();
    expect(screen.getByText(/رمز عبور فعلی شما موقت است/)).toBeInTheDocument();
  });

  it("Login_Succeeds_GoesWhereTheUserWasHeading", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/auth/login": () => json(200, session()),
    });
    renderApp("/");

    fillAndSubmit("sara", "Pass1234");

    expect(await screen.findByText("سالم")).toBeInTheDocument();
  });

  it("Login_PersianDigitsInThePassword_AreSentAsEnglishDigits", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/auth/login": () => json(200, session()),
    });
    renderApp("/login");

    fillAndSubmit("sara", "رمز۱۲۳۴");

    await screen.findByText("سالم");
    const body = (await api.requestsTo("POST", "/api/auth/login")[0]!.json()) as {
      password: string;
    };
    expect(body.password).toBe("رمز1234");
  });
});
