import { fireEvent, screen, waitFor } from "@testing-library/react";

import { chooseBirthDate } from "@/test/jalaliDate";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

/** Every member has a birth date, so a test about something else gets one chosen for it. */
function fill({ fullName = "", phoneNumber = "", notes = "", birthDate = "1370/05/12" }) {
  fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), { target: { value: fullName } });
  fireEvent.change(screen.getByLabelText("شماره موبایل"), { target: { value: phoneNumber } });
  if (birthDate !== "") {
    chooseBirthDate(birthDate);
  }
  fireEvent.change(screen.getByLabelText("یادداشت (اختیاری)"), { target: { value: notes } });
  fireEvent.click(screen.getByRole("button", { name: "ثبت عضو" }));
}

describe("CreateMemberPage", () => {
  it("CreateMember_ValidForm_SendsNormalizedValuesAndOpensTheProfile", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/members": () => json(201, reza),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });
    const { router } = renderApp("/members/new", { session: session() });

    // Arabic ye (U+064A), extra spaces, Persian digits in the phone.
    fill({
      fullName: "  رضا   احمد\u064A ",
      phoneNumber: "۰۹۱۲ ۱۲۳ ۴۵۶۷",
      birthDate: "1370/05/12",
      notes: "عضو قدیمی",
    });

    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${reza.id}`));
    const body = (await api.requestsTo("POST", "/api/members")[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body).toEqual({
      fullName: "رضا احمدی",
      phoneNumber: "0912 123 4567",
      // Typed Jalali, sent Gregorian (docs/BUSINESS_RULES.md §13).
      birthDate: "1991-08-03",
      notes: "عضو قدیمی",
    });
    expect(await screen.findByText("رضا احمدی")).toBeInTheDocument();
  });

  it("CreateMember_BlankNotes_AreSentAsNull", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/members": () => json(201, reza),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });
    renderApp("/members/new", { session: session() });

    fill({ fullName: "رضا", phoneNumber: "09121234567", notes: "   " });

    await waitFor(() => expect(api.requestsTo("POST", "/api/members")).toHaveLength(1));
    const body = (await api.requestsTo("POST", "/api/members")[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body.notes).toBeNull();
  });

  it("CreateMember_BlankBirthDate_IsRejectedBeforeSending", async () => {
    // Required (docs/BUSINESS_RULES.md §2): the gym greets members on their birthday.
    const api = mockApi(signedInHandlers(staffUser));
    renderApp("/members/new", { session: session() });

    fill({ fullName: "رضا", phoneNumber: "09121234567", birthDate: "" });

    const message = await screen.findByText("تاریخ تولد را وارد کنید.");
    expect(screen.getByLabelText("تاریخ تولد")).toHaveAttribute("aria-describedby", message.id);
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(0);
  });

  it("CreateMember_ServerRejectsTheBirthDate_ShowsTheReasonUnderTheBirthDate", async () => {
    // The rule is judged against the gym's today, which only the API knows for certain, so the
    // failure arrives as a whole-request error and codeFields puts it under the right box.
    mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/members": () => problem(400, "Members.BirthDateInFuture"),
    });
    renderApp("/members/new", { session: session() });

    fill({ fullName: "رضا", phoneNumber: "09121234567", birthDate: "1370/05/12" });

    const message = await screen.findByText("تاریخ تولد نمی‌تواند در آینده باشد.");
    expect(screen.getByLabelText("تاریخ تولد")).toHaveAttribute("aria-describedby", message.id);
  });

  it("CreateMember_BlankNameAndPhone_AreRejectedBeforeSending", async () => {
    const api = mockApi(signedInHandlers(staffUser));
    renderApp("/members/new", { session: session() });

    fill({ fullName: "   ", phoneNumber: "" });

    expect(await screen.findByText("نام و نام خانوادگی را وارد کنید.")).toBeInTheDocument();
    expect(screen.getByText("شماره موبایل را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(0);
  });

  it.each([
    [409, "Members.PhoneAlreadyExists", "این شماره موبایل قبلاً برای عضو دیگری ثبت شده است."],
    [400, "Members.PhoneNotMobile", "شماره باید موبایل باشد؛ شمارهٔ ثابت پذیرفته نمی‌شود."],
    [400, "Members.PhoneNotIranian", "فقط شماره موبایل ایران پذیرفته می‌شود."],
  ])(
    "CreateMember_ServerRejectsThePhone_ShowsTheReasonUnderThePhone (%s %s)",
    async (status, code, text) => {
      mockApi({
        ...signedInHandlers(staffUser),
        "POST /api/members": () => problem(status, code),
      });
      const { router } = renderApp("/members/new", { session: session() });

      fill({ fullName: "رضا", phoneNumber: "09121234567" });

      const message = await screen.findByText(text);
      expect(screen.getByLabelText("شماره موبایل")).toHaveAttribute("aria-describedby", message.id);
      expect(router.state.location.pathname).toBe("/members/new");
    },
  );

  it("CreateMember_ServerFieldErrors_AppearUnderTheirFields", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      "POST /api/members": () =>
        problem(400, "General.ValidationFailed", {
          errors: { fullName: [{ code: "Members.FullNameTooLong", description: "too long" }] },
        }),
    });
    renderApp("/members/new", { session: session() });

    fill({ fullName: "رضا", phoneNumber: "09121234567" });

    const message = await screen.findByText("نام بیش از حد طولانی است.");
    expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveAttribute(
      "aria-describedby",
      message.id,
    );
  });
});
