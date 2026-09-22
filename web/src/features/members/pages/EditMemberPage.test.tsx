import { fireEvent, screen, waitFor } from "@testing-library/react";

import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

const editPath = `/members/${reza.id}/edit`;

describe("EditMemberPage", () => {
  it("EditMember_Loaded_ShowsTheCurrentValues", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
    });

    renderApp(editPath, { session: session() });

    expect(await screen.findByLabelText("نام و نام خانوادگی")).toHaveValue("رضا احمدی");
    expect(screen.getByLabelText("شماره موبایل")).toHaveValue("+989121234567");
    expect(screen.getByLabelText("یادداشت (اختیاری)")).toHaveValue("عضو قدیمی");
    // Stored Gregorian, shown Jalali (docs/BUSINESS_RULES.md §13).
    expect(screen.getByLabelText("تاریخ تولد (اختیاری)")).toHaveValue("۱۳۷۰/۰۵/۱۲");
  });

  it("EditMember_Saved_SendsTheVersionItWasFilledFromAndOpensTheProfile", async () => {
    const saved = { ...reza, fullName: "رضا احمدی‌نژاد", version: 6 };
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`PUT /api/members/${reza.id}`]: () => json(200, saved),
    });
    const { router } = renderApp(editPath, { session: session() });

    fireEvent.change(await screen.findByLabelText("نام و نام خانوادگی"), {
      target: { value: "رضا احمدی نژاد" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${reza.id}`));
    const body = (await api.requestsTo("PUT", `/api/members/${reza.id}`)[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body).toEqual({
      fullName: "رضا احمدی نژاد",
      phoneNumber: "+989121234567",
      birthDate: "1991-08-03",
      notes: "عضو قدیمی",
      version: 5,
    });
    // The profile shows the saved member straight from the PUT answer.
    expect(await screen.findByText("رضا احمدی‌نژاد")).toBeInTheDocument();
  });

  it("EditMember_BirthDateCleared_IsSentAsNull", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`PUT /api/members/${reza.id}`]: () => json(200, { ...reza, birthDate: null, version: 6 }),
    });
    renderApp(editPath, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "پاک کردن تاریخ" }));
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    await waitFor(() => expect(api.requestsTo("PUT", `/api/members/${reza.id}`)).toHaveLength(1));
    const body = (await api.requestsTo("PUT", `/api/members/${reza.id}`)[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body.birthDate).toBeNull();
  });

  it("EditMember_SomeoneSavedMeanwhile_ExplainsAndReloadsTheirVersion", async () => {
    const theirs = { ...reza, fullName: "رضا احمدی (ویرایش همکار)", version: 6 };
    let current = reza;
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, current),
      [`PUT /api/members/${reza.id}`]: () => problem(409, "Members.ChangedConcurrently"),
    });
    const { router } = renderApp(editPath, { session: session() });

    fireEvent.change(await screen.findByLabelText("نام و نام خانوادگی"), {
      target: { value: "نام من" },
    });
    // A colleague saves first.
    current = theirs;
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText(/هم‌زمان توسط شخص دیگری ویرایش شد/)).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(editPath);

    fireEvent.click(screen.getByRole("button", { name: "بارگذاری اطلاعات تازه" }));

    await waitFor(() =>
      expect(screen.getByLabelText("نام و نام خانوادگی")).toHaveValue(theirs.fullName),
    );
    expect(screen.queryByText(/هم‌زمان توسط شخص دیگری ویرایش شد/)).not.toBeInTheDocument();
    expect(api.requestsTo("GET", `/api/members/${reza.id}`)).toHaveLength(2);
  });

  it("EditMember_InactiveMember_CanBeEdited", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${ali.id}`]: () => json(200, ali),
      [`PUT /api/members/${ali.id}`]: () => json(200, { ...ali, version: 8 }),
    });
    const { router } = renderApp(`/members/${ali.id}/edit`, { session: session() });

    fireEvent.change(await screen.findByLabelText("شماره موبایل"), {
      target: { value: "09351112233" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/members/${ali.id}`));
  });

  it("EditMember_UnknownId_SaysTheMemberWasNotFound", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => problem(404, "Members.NotFound"),
    });

    renderApp(editPath, { session: session() });

    expect(await screen.findByText("عضو پیدا نشد.")).toBeInTheDocument();
  });
});
