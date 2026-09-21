import { fireEvent, screen, waitFor } from "@testing-library/react";

import {
  attendanceHistoryPage,
  autoClosedVisit,
  cancelledVisit,
  closedVisit,
  openVisit,
  openVisitNoLocker,
} from "@/test/attendance";
import { json, mockApi, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { ali, reza } from "@/test/members";
import { renderApp } from "@/test/renderApp";

describe("MemberProfilePage", () => {
  it("Profile_Loaded_ShowsNamePhoneNotesAndStatus", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("رضا احمدی")).toBeInTheDocument();
    expect(screen.getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toHaveAttribute("dir", "ltr");
    expect(screen.getByText("عضو قدیمی")).toBeInTheDocument();
    expect(screen.getByText("فعال")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "ویرایش" })).toHaveAttribute(
      "href",
      `/members/${reza.id}/edit`,
    );
  });

  it("Profile_Deactivate_CallsTheApiAndShowsTheMemberAsInactive", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`POST /api/members/${reza.id}/deactivate`]: () =>
        json(200, { ...reza, isActive: false, version: 6 }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("عضو غیرفعال شد.");
    expect(screen.getByText("غیرفعال")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "فعال‌سازی" })).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/deactivate`)).toHaveLength(1);
  });

  it("Profile_Reactivate_CallsTheApiAndShowsTheMemberAsActive", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${ali.id}`]: () => json(200, ali),
      [`GET /api/members/${ali.id}/attendance`]: () => attendanceHistoryPage([]),
      [`POST /api/members/${ali.id}/reactivate`]: () =>
        json(200, { ...ali, isActive: true, version: 8 }),
    });
    renderApp(`/members/${ali.id}`, { session: session() });

    expect(await screen.findByText(/نمی‌تواند وارد باشگاه شود/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "فعال‌سازی" }));

    expect(await screen.findByRole("status")).toHaveTextContent("عضو دوباره فعال شد.");
    expect(screen.getByText("فعال")).toBeInTheDocument();
    expect(screen.queryByText(/نمی‌تواند وارد باشگاه شود/)).not.toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${ali.id}/reactivate`)).toHaveLength(1);
  });

  it("Profile_DeactivateFails_ShowsThePersianError", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`POST /api/members/${reza.id}/deactivate`]: () =>
        problem(409, "Members.ChangedConcurrently"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "غیرفعال‌سازی" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("هم‌زمان توسط شخص دیگری ویرایش شد");
  });

  it("Profile_UnknownId_SaysTheMemberWasNotFoundWithoutRetrying", async () => {
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => problem(404, "Members.NotFound"),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("عضو پیدا نشد.")).toBeInTheDocument();
    expect(api.requestsTo("GET", `/api/members/${reza.id}`)).toHaveLength(1);
  });

  // ---- Attendance (docs/ROADMAP.md 5.6) ----

  it("Profile_NoOpenVisit_ShowsACheckInButton", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () =>
        attendanceHistoryPage([closedVisit(reza.id)]),
    });

    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByRole("button", { name: "ورود" })).toBeInTheDocument();
    expect(screen.queryByText("هم‌اکنون داخل باشگاه است.")).not.toBeInTheDocument();
  });

  it("Profile_CheckIn_ShowsTheAssignedLocker", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, visit),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("کمد شماره ۳");
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(1);
  });

  it("Profile_CheckInWithNoFreeLocker_ShowsTheWarning", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        json(201, openVisitNoLocker(reza.id)),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("کمد آزادی نبود");
  });

  it("Profile_CheckInFails_ShowsThePersianReason", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([]),
      [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
        problem(422, "Attendance.NoSubscription"),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "ورود" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("این عضو اشتراکی ندارد.");
  });

  it("Profile_OpenVisit_ShowsCheckedInStateAndCanCheckOut", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([visit]),
      [`POST /api/attendance/${visit.id}/check-out`]: () =>
        json(200, { ...visit, checkedOutAt: "2026-09-18T09:00:00Z" }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("هم‌اکنون داخل باشگاه است.")).toBeInTheDocument();
    expect(screen.getByText(/کمد ۳/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "ثبت خروج" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${visit.id}/check-out`)).toHaveLength(1),
    );
  });

  it("Profile_OpenVisit_CanCancelCheckIn", async () => {
    const visit = openVisit(reza.id);
    const api = mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () => attendanceHistoryPage([visit]),
      [`POST /api/attendance/${visit.id}/cancel`]: () =>
        json(200, { ...visit, checkedOutAt: "2026-09-18T07:05:00Z", cancelledAt: "2026-09-18T07:05:00Z" }),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "لغو ورود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("جلسه به اشتراک بازگشت");
    expect(api.requestsTo("POST", `/api/attendance/${visit.id}/cancel`)).toHaveLength(1);
  });

  it("Profile_History_ShowsCancelledAndAutoClosedStatuses", async () => {
    mockApi({
      ...signedInHandlers(staffUser),
      [`GET /api/members/${reza.id}`]: () => json(200, reza),
      [`GET /api/members/${reza.id}/attendance`]: () =>
        attendanceHistoryPage([cancelledVisit(reza.id), autoClosedVisit(reza.id)]),
    });
    renderApp(`/members/${reza.id}`, { session: session() });

    expect(await screen.findByText("لغو شده")).toBeInTheDocument();
    expect(screen.getByText("بسته خودکار")).toBeInTheDocument();
  });
});
