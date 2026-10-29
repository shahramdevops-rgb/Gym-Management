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
    expect(screen.queryByRole("heading", { name: "درخواست‌ها" })).not.toBeInTheDocument();
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

    expect(await screen.findByRole("list", { name: "شاگردان من" })).toHaveTextContent("علی محمدی");
    expect(screen.queryByRole("heading", { name: "درخواست‌ها" })).not.toBeInTheDocument();
    expect(
      api.requestsTo("POST", `/api/site/coaching/requests/${aliRequest.requestId}/accept`),
    ).toHaveLength(1);
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
      expect(screen.queryByRole("heading", { name: "درخواست‌ها" })).not.toBeInTheDocument(),
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

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "این درخواست دیگر در انتظار پاسخ نیست",
    );
    await vi.waitFor(() =>
      expect(screen.queryByRole("button", { name: "پذیرفتن" })).not.toBeInTheDocument(),
    );
  });

  it("Trainer_EndStudent_AsksFirst", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, trainerOnly),
      "GET /api/site/coaching/students": () => json(200, { requests: [], students: [ali] }),
      [`POST /api/site/coaching/students/${ali.memberId}/end`]: () =>
        json(200, { requests: [], students: [] }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });

    fireEvent.click(
      within(await screen.findByRole("list", { name: "شاگردان من" })).getByRole("button", {
        name: "پایان",
      }),
    );
    const question = await screen.findByRole("dialog");
    expect(question).toHaveTextContent(
      "مربی‌گری شما برای علی محمدی از امروز تمام می‌شود. ادامه می‌دهید؟",
    );
    fireEvent.click(within(question).getByRole("button", { name: "پایان" }));

    expect(await screen.findByText("هنوز شاگردی ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/site/coaching/students/${ali.memberId}/end`)).toHaveLength(
      1,
    );
  });
});
