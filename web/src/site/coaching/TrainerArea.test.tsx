import { fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi, problem } from "@/test/mockApi";

import { renderSite, siteAccess, trainer } from "../test/renderSite";

const trainerOnly = { member: null, trainer };

const aliRequest = {
  requestId: "0199a000-0000-7000-8000-000000000301",
  memberId: "0199a000-0000-7000-8000-000000000101",
  fullName: "علی محمدی",
  phoneNumber: "+989121234567",
  requestedOn: "2026-10-29",
};
const ali = {
  memberId: aliRequest.memberId,
  fullName: "علی محمدی",
  phoneNumber: "+989121234567",
  since: "2026-10-29",
  isActive: true,
};
const neda = {
  memberId: "0199a000-0000-7000-8000-000000000102",
  fullName: "ندا حسینی",
  phoneNumber: "+989129876543",
  since: "2026-10-01",
  isActive: false,
};

function list(name: string) {
  return within(screen.getByRole("list", { name }));
}

describe("TrainerArea", () => {
  it("Trainer_NoRequests_ShowsOnlyTheirStudents", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [], students: [ali, neda] }),
    });

    renderSite("/", { access: siteAccess(trainerOnly) });

    await screen.findByRole("list", { name: "شاگردان من" });
    expect(screen.queryByRole("heading", { name: /^درخواست‌ها/ })).not.toBeInTheDocument();
    expect(list("شاگردان من").getByRole("link", { name: "۰۹۱۲ ۱۲۳ ۴۵۶۷" })).toHaveAttribute(
      "href",
      "tel:+989121234567",
    );
    expect(list("شاگردان من").getByText("غیرفعال")).toBeInTheDocument();
  });

  it("Trainer_Accept_MovesTheMemberToTheirStudents", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [aliRequest], students: [] }),
      [`POST /api/site/coaching/requests/${aliRequest.requestId}/accept`]: () =>
        json(200, { requests: [], students: [ali] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByText("هنوز شاگردی ندارید.")).toBeInTheDocument();
    expect(list("درخواست‌ها").getByText("علی محمدی")).toBeInTheDocument();
    fireEvent.click(list("درخواست‌ها").getByRole("button", { name: "پذیرفتن" }));
    // The coaching period's days (15.7h): 30 to start with.
    expect(list("درخواست‌ها").getByRole("textbox", { name: "دورهٔ مربی‌گری" })).toHaveValue("۳۰");
    fireEvent.click(list("درخواست‌ها").getByRole("button", { name: "پذیرفتن" }));

    expect(await screen.findByRole("list", { name: "شاگردان من" })).toHaveTextContent("علی محمدی");
    expect(screen.queryByRole("heading", { name: /^درخواست‌ها/ })).not.toBeInTheDocument();
    expect(
      api.requestsTo("POST", `/api/site/coaching/requests/${aliRequest.requestId}/accept`),
    ).toHaveLength(1);
    const [sent] = api.requestsTo(
      "POST",
      `/api/site/coaching/requests/${aliRequest.requestId}/accept`,
    );
    expect(await sent!.json()).toEqual({ days: 30 });
  });

  it("Trainer_Decline_AsksNothing", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [aliRequest], students: [] }),
      [`POST /api/site/coaching/requests/${aliRequest.requestId}/decline`]: () =>
        json(200, { requests: [], students: [] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    fireEvent.click(
      within(await screen.findByRole("list", { name: "درخواست‌ها" })).getByRole("button", {
        name: "رد کردن",
      }),
    );

    await vi.waitFor(() =>
      expect(screen.queryByRole("heading", { name: /^درخواست‌ها/ })).not.toBeInTheDocument(),
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(
      api.requestsTo("POST", `/api/site/coaching/requests/${aliRequest.requestId}/decline`),
    ).toHaveLength(1);
  });

  it("Trainer_RequestCancelledMeanwhile_SaysSoAndRefreshes", async () => {
    let current: unknown = { requests: [aliRequest], students: [] };
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, current),
      [`POST /api/site/coaching/requests/${aliRequest.requestId}/accept`]: () => {
        current = { requests: [], students: [] };
        return problem(409, "Coaching.RequestNoLongerPending");
      },
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    fireEvent.click(
      within(await screen.findByRole("list", { name: "درخواست‌ها" })).getByRole("button", {
        name: "پذیرفتن",
      }),
    );
    fireEvent.click(list("درخواست‌ها").getByRole("button", { name: "پذیرفتن" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "این درخواست دیگر در انتظار پاسخ نیست",
    );
    await vi.waitFor(() =>
      expect(screen.queryByRole("button", { name: "پذیرفتن" })).not.toBeInTheDocument(),
    );
  });

  it("Trainer_WaitingRequests_CountedAndFirstOnTheHome", async () => {
    const second = {
      ...aliRequest,
      requestId: "0199a000-0000-7000-8000-000000000302",
      fullName: "رضا کریمی",
    };
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () =>
        json(200, { requests: [aliRequest, second], students: [neda] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const heading = await screen.findByRole("heading", { name: "درخواست‌ها (۲)", level: 2 });
    const headings = screen.getAllByRole("heading", { level: 2 });
    expect(headings[0]).toBe(heading);
  });

  it("Trainer_SearchByName_ShowsOnlyThoseStudents", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [], students: [ali, neda] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });
    await screen.findByRole("list", { name: "شاگردان من" });

    fireEvent.change(screen.getByRole("searchbox", { name: "جست‌وجوی شاگرد" }), {
      target: { value: "ندا" },
    });

    const rows = list("شاگردان من").getAllByRole("listitem");
    expect(rows).toHaveLength(1);
    expect(rows[0]).toHaveTextContent("ندا حسینی");
  });

  it("Trainer_SearchByPersianDigits_FindsThePhone", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [], students: [ali, neda] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });
    await screen.findByRole("list", { name: "شاگردان من" });

    fireEvent.change(screen.getByRole("searchbox", { name: "جست‌وجوی شاگرد" }), {
      target: { value: "۰۹۱۲۱۲۳" },
    });

    expect(list("شاگردان من").getAllByRole("listitem")).toHaveLength(1);
    expect(list("شاگردان من").getByText("علی محمدی")).toBeInTheDocument();
  });

  it("Trainer_SearchMatchingNoOne_SaysSo", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [], students: [ali, neda] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });
    await screen.findByRole("list", { name: "شاگردان من" });

    fireEvent.change(screen.getByRole("searchbox", { name: "جست‌وجوی شاگرد" }), {
      target: { value: "سعید" },
    });

    expect(screen.getByText("شاگردی با این نام یا شماره پیدا نشد.")).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "شاگردان من" })).not.toBeInTheDocument();
  });

  it("Trainer_Home_HasNoEndButton", async () => {
    mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [], students: [ali] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    const students = within(await screen.findByRole("list", { name: "شاگردان من" }));

    // «پایان مربی‌گری» is at the foot of «برنامهٔ ثابت» since 15.7g.
    expect(students.queryByRole("button", { name: /پایان/ })).not.toBeInTheDocument();
  });
});
