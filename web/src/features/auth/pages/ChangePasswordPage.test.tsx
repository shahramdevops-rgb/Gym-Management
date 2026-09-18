import { fireEvent, screen } from "@testing-library/react";

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

    fill("Temp1234", "Chosen5678", "Chosen5679");

    expect(await screen.findByText("تکرار رمز عبور با رمز جدید یکسان نیست.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/auth/change-password")).toHaveLength(0);
  });

  it("ChangePassword_WeakNewPassword_ShowsThePolicyMessage", async () => {
    mockApi({});
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Temp1234", "onlyletters");

    expect(
      await screen.findByText("رمز عبور باید دست‌کم یک حرف و یک رقم داشته باشد."),
    ).toBeInTheDocument();
  });

  it("ChangePassword_WrongCurrentPassword_ShowsTheErrorUnderThatField", async () => {
    mockApi({
      "POST /api/auth/change-password": () => problem(400, "Auth.CurrentPasswordIncorrect"),
    });
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Wrong1234", "Chosen5678");

    const message = await screen.findByText("رمز عبور فعلی اشتباه است.");
    expect(screen.getByLabelText("رمز عبور فعلی")).toHaveAttribute("aria-describedby", message.id);
  });

  it("ChangePassword_Succeeds_StartsTheNewSessionAndOpensTheApp", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "POST /api/auth/change-password": () => json(200, session({ accessToken: "after-change" })),
    });
    renderApp("/change-password", { session: session({ mustChangePassword: true }) });

    fill("Temp1234", "Chosen۵۶۷۸");

    expect(await screen.findByRole("link", { name: "کارمندان" })).toBeInTheDocument();
    expect(sessionStore.accessToken()).toBe("after-change");
    const body = (await api.requestsTo("POST", "/api/auth/change-password")[0]!.json()) as {
      newPassword: string;
    };
    expect(body.newPassword).toBe("Chosen5678");
  });
});
