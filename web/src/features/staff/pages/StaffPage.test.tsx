import { fireEvent, screen, within } from "@testing-library/react";

import type { StaffMember } from "@/features/staff/api";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const reza: StaffMember = {
  id: "0199a000-0000-7000-8000-00000000000a",
  userName: "reza",
  fullName: "رضا احمدی",
  isActive: true,
  mustChangePassword: true,
  isLockedOut: false,
};

const mina: StaffMember = {
  id: "0199a000-0000-7000-8000-00000000000b",
  userName: "mina",
  fullName: "مینا کریمی",
  isActive: false,
  mustChangePassword: false,
  isLockedOut: true,
};

function page(items: StaffMember[], totalCount = items.length) {
  return json(200, { items, page: 1, pageSize: 20, totalCount });
}

describe("StaffPage", () => {
  it("StaffPage_StaffUser_SeesNoAccessMessage", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/staff", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/staff")).toHaveLength(0);
  });

  it("StaffPage_Owner_ListsStaffWithTheirStatus", async () => {
    mockApi({ ...signedInHandlers(owner), "GET /api/staff": () => page([reza, mina]) });

    renderApp("/staff", { session: session() });

    const rezaRow = (await screen.findByText("رضا احمدی")).closest("tr")!;
    expect(within(rezaRow).getByText("فعال")).toBeInTheDocument();
    expect(within(rezaRow).getByText("رمز موقت")).toBeInTheDocument();

    const minaRow = screen.getByText("مینا کریمی").closest("tr")!;
    expect(within(minaRow).getByText("غیرفعال")).toBeInTheDocument();
    expect(within(minaRow).getByText("قفل‌شده")).toBeInTheDocument();
    expect(within(minaRow).getByRole("button", { name: "فعال‌سازی" })).toBeInTheDocument();
  });

  it("StaffPage_ManyStaff_ShowsPersianPageNumbers", async () => {
    mockApi({ ...signedInHandlers(owner), "GET /api/staff": () => page([reza], 45) });

    renderApp("/staff", { session: session() });

    expect(await screen.findByText("صفحهٔ ۱ از ۳")).toBeInTheDocument();
  });

  it("CreateStaff_ValidForm_SendsNormalizedValuesAndShowsConfirmation", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/staff": () => page([]),
      "POST /api/staff": () => json(201, reza),
    });
    renderApp("/staff", { session: session() });
    await screen.findByText("هنوز هیچ کارمندی ثبت نشده است.");

    fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), {
      target: { value: "  رضا   احمد\u064A " },
    });
    fireEvent.change(screen.getByLabelText("نام کاربری"), { target: { value: "reza" } });
    fireEvent.change(screen.getByLabelText("رمز عبور موقت"), { target: { value: "Temp۱۲۳۴" } });
    fireEvent.click(screen.getByRole("button", { name: "ساخت حساب" }));

    expect(await screen.findByRole("status")).toHaveTextContent("حساب «رضا احمدی» ساخته شد.");
    const body = (await api.requestsTo("POST", "/api/staff")[0]!.json()) as Record<string, string>;
    // Arabic ye (U+064A) becomes Persian ye (U+06CC), spaces collapse, Persian digits become English.
    expect(body).toEqual({
      userName: "reza",
      fullName: "رضا احمدی",
      temporaryPassword: "Temp1234",
    });
  });

  it("CreateStaff_UserNameTaken_ShowsTheErrorUnderTheUserName", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/staff": () => page([]),
      "POST /api/staff": () => problem(409, "Staff.UserNameTaken"),
    });
    renderApp("/staff", { session: session() });
    await screen.findByText("هنوز هیچ کارمندی ثبت نشده است.");

    fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), { target: { value: "رضا" } });
    fireEvent.change(screen.getByLabelText("نام کاربری"), { target: { value: "reza" } });
    fireEvent.change(screen.getByLabelText("رمز عبور موقت"), { target: { value: "Temp1234" } });
    fireEvent.click(screen.getByRole("button", { name: "ساخت حساب" }));

    const message = await screen.findByText("این نام کاربری قبلاً استفاده شده است.");
    expect(screen.getByLabelText("نام کاربری")).toHaveAttribute("aria-describedby", message.id);
  });

  it("CreateStaff_PersianUserName_IsRejectedBeforeSending", async () => {
    const api = mockApi({ ...signedInHandlers(owner), "GET /api/staff": () => page([]) });
    renderApp("/staff", { session: session() });
    await screen.findByText("هنوز هیچ کارمندی ثبت نشده است.");

    fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), { target: { value: "رضا" } });
    fireEvent.change(screen.getByLabelText("نام کاربری"), { target: { value: "رضا" } });
    fireEvent.change(screen.getByLabelText("رمز عبور موقت"), { target: { value: "Temp1234" } });
    fireEvent.click(screen.getByRole("button", { name: "ساخت حساب" }));

    expect(await screen.findByText(/فقط می‌تواند حروف انگلیسی/)).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/staff")).toHaveLength(0);
  });

  it("Deactivate_ActiveStaff_CallsTheApiAndConfirms", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/staff": () => page([reza]),
      [`POST /api/staff/${reza.id}/deactivate`]: () => json(200, { ...reza, isActive: false }),
    });
    renderApp("/staff", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("غیرفعال شد");
    expect(api.requestsTo("POST", `/api/staff/${reza.id}/deactivate`)).toHaveLength(1);
  });

  it("ResetPassword_NewTemporaryPassword_CallsTheApiAndConfirms", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/staff": () => page([reza]),
      [`POST /api/staff/${reza.id}/reset-password`]: () => new Response(null, { status: 204 }),
    });
    renderApp("/staff", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "بازنشانی رمز" }));
    fireEvent.change(screen.getByLabelText(/رمز عبور موقت تازه/), {
      target: { value: "Fresh5678" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByRole("status")).toHaveTextContent("بازنشانی شد");
    const body = (await api
      .requestsTo("POST", `/api/staff/${reza.id}/reset-password`)[0]!
      .json()) as Record<string, string>;
    expect(body).toEqual({ temporaryPassword: "Fresh5678" });
  });
});
