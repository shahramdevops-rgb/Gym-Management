import { fireEvent, screen, within } from "@testing-library/react";

import { sessionStore } from "@/features/auth/session";
import { json, mockApi, owner, problem, session, signedInHandlers } from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

function fill(current: string, next: string, confirm = next) {
  fireEvent.change(screen.getByLabelText("رمز عبور فعلی"), { target: { value: current } });
  fireEvent.change(screen.getByLabelText("رمز عبور جدید"), { target: { value: next } });
  fireEvent.change(screen.getByLabelText("تکرار رمز عبور جدید"), { target: { value: confirm } });
  fireEvent.click(screen.getByRole("button", { name: "ذخیرهٔ رمز عبور" }));
}

describe("ChangePasswordPage", () => {
  it("Gate_UserWithTemporaryPassword_IsSentToChangePasswordFromAnyPage", () => {
    mockApi({});

    renderApp("/staff", { session: session({ mustChangePassword: true }) });

    expect(screen.getByRole("heading", { name: "تغییر رمز عبور" })).toBeInTheDocument();
    // Nowhere else to go yet, so no menu items.
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("ChangePassword_ConfirmationDiffers_ShowsErrorWithoutCallingTheApi", async () => {
    const api = mockApi({});
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Temp1234", "chosen kettle 5678", "chosen kettle 5679");

    expect(await screen.findByText("تکرار رمز عبور با رمز جدید یکسان نیست.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/auth/change-password")).toHaveLength(0);
  });

  it("ChangePassword_ShortNewPassword_ShowsThePolicyMessageWithoutCallingTheApi", async () => {
    const api = mockApi({});
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Temp1234", "short1");

    expect(await screen.findByText("رمز عبور باید دست‌کم ۱۲ نویسه باشد.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/auth/change-password")).toHaveLength(0);
  });

  it("ChangePassword_Checklist_TurnsGreenRuleByRuleAsTheUserTypes", () => {
    mockApi({});
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });
    const checklist = screen.getByRole("list", { name: "شرایط رمز عبور" });
    const rule = (text: RegExp) => within(checklist).getByText(text).closest("li")!;

    fireEvent.change(screen.getByLabelText("رمز عبور جدید"), { target: { value: "kettle" } });
    expect(rule(/دست‌کم ۱۲ نویسه/)).toHaveAttribute("data-met", "false");
    expect(rule(/فقط حروف و علامت‌های انگلیسی/)).toHaveAttribute("data-met", "true");

    fireEvent.change(screen.getByLabelText("رمز عبور جدید"), {
      target: { value: "kettle under the lamp" },
    });
    expect(rule(/دست‌کم ۱۲ نویسه/)).toHaveAttribute("data-met", "true");
    expect(rule(/تکراری یا پشت سر هم/)).toHaveAttribute("data-met", "true");

    fireEvent.change(screen.getByLabelText("رمز عبور جدید"), { target: { value: "123456789012" } });
    expect(rule(/تکراری یا پشت سر هم/)).toHaveAttribute("data-met", "false");
  });

  it("ChangePassword_CommonPasswordRefusedByTheServer_ShowsTheErrorUnderTheNewPassword", async () => {
    // Only the server has the blocklist, and Identity reports it as a whole-request error.
    mockApi({
      "POST /api/auth/change-password": () => problem(400, "Auth.PasswordTooCommon"),
    });
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Temp1234", "Football2024!");

    const message = await screen.findByText(/جزو رمزهای رایج و لو رفته است/);
    expect(screen.getByLabelText("رمز عبور جدید")).toHaveAttribute(
      "aria-describedby",
      expect.stringContaining(message.id),
    );
  });

  it("PasswordField_PersianLetters_WarnsThatTheKeyboardIsPersian", () => {
    mockApi({});
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fireEvent.change(screen.getByLabelText("رمز عبور جدید"), { target: { value: "۱۲۳۴" } });
    expect(screen.queryByText(/کیبورد روی فارسی است/)).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("رمز عبور جدید"), { target: { value: "سلام" } });
    expect(screen.getByText(/کیبورد روی فارسی است/)).toBeInTheDocument();
  });

  it("PasswordField_EyeButton_ShowsAndHidesThePassword", () => {
    mockApi({});
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });
    const input = screen.getByLabelText("رمز عبور جدید");

    expect(input).toHaveAttribute("type", "password");
    fireEvent.click(screen.getAllByRole("button", { name: "نمایش رمز عبور" })[1]!);
    expect(input).toHaveAttribute("type", "text");
    fireEvent.click(screen.getByRole("button", { name: "پنهان کردن رمز عبور" }));
    expect(input).toHaveAttribute("type", "password");
  });

  it("ChangePassword_WrongCurrentPassword_ShowsTheErrorUnderThatField", async () => {
    mockApi({
      "POST /api/auth/change-password": () => problem(400, "Auth.CurrentPasswordIncorrect"),
    });
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Wrong1234", "chosen kettle 5678");

    const message = await screen.findByText("رمز عبور فعلی اشتباه است.");
    expect(screen.getByLabelText("رمز عبور فعلی")).toHaveAttribute("aria-describedby", message.id);
  });

  it("ChangePassword_Succeeds_StartsTheNewSessionAndOpensTheApp", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "POST /api/auth/change-password": () => json(200, session({ accessToken: "after-change" })),
    });
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Temp1234", "chosen kettle ۵۶۷۸");

    expect(await screen.findByRole("link", { name: "کارمندان" })).toBeInTheDocument();
    expect(sessionStore.accessToken()).toBe("after-change");
    const body = (await api.requestsTo("POST", "/api/auth/change-password")[0]!.json()) as {
      newPassword: string;
    };
    expect(body.newPassword).toBe("chosen kettle 5678");
  });
});
