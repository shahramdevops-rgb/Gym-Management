import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { gymToday } from "@/lib/format";
import {
  json,
  mockApi,
  owner,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

import type { AuditLog, AuditUser } from "../api";

const ownerAccount: AuditUser = {
  id: "01990000-0000-7000-8000-0000000000c1",
  fullName: "مدیر باشگاه",
};
const staffAccount: AuditUser = {
  id: "01990000-0000-7000-8000-0000000000c2",
  fullName: "رضا احمدی",
};

const saraId = "01990000-0000-7000-8000-0000000000a1";

const usedSession: AuditLog = {
  id: "01990000-0000-7000-8000-000000000001",
  occurredAt: "2026-10-10T06:30:00Z",
  action: "Update",
  entityType: "Subscription",
  entityId: "01990000-0000-7000-8000-0000000000d1",
  userId: staffAccount.id,
  userFullName: staffAccount.fullName,
  ipAddress: "10.0.0.5",
  memberId: saraId,
  memberFullName: "سارا محمدی",
  changes: [{ field: "UsedSessions", oldValue: 4, newValue: 5 }],
};

const payment: AuditLog = {
  id: "01990000-0000-7000-8000-000000000002",
  occurredAt: "2026-10-10T06:20:00Z",
  action: "Insert",
  entityType: "Payment",
  entityId: "01990000-0000-7000-8000-0000000000d2",
  userId: ownerAccount.id,
  userFullName: ownerAccount.fullName,
  ipAddress: null,
  memberId: saraId,
  memberFullName: "سارا محمدی",
  changes: [
    { field: "Id", oldValue: null, newValue: "01990000-0000-7000-8000-0000000000d2" },
    { field: "Amount", oldValue: null, newValue: 180000 },
    { field: "Method", oldValue: null, newValue: "Card" },
    { field: "ReceivedByUserId", oldValue: null, newValue: ownerAccount.id },
  ],
};

const job: AuditLog = {
  id: "01990000-0000-7000-8000-000000000003",
  occurredAt: "2026-10-10T06:10:00Z",
  action: "Update",
  entityType: "Locker",
  entityId: "01990000-0000-7000-8000-0000000000d3",
  userId: null,
  userFullName: null,
  ipAddress: null,
  memberId: null,
  memberFullName: null,
  changes: [{ field: "IsOutOfService", oldValue: false, newValue: true }],
};

function logsPage(items: AuditLog[]) {
  return json(200, { items, page: 1, pageSize: 20, totalCount: items.length });
}

function ownerWith(extra: Record<string, Handler> = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "GET /api/audit-logs": () => logsPage([usedSession, payment, job]),
    "GET /api/audit-logs/users": () => json(200, [ownerAccount, staffAccount]),
    ...extra,
  });
}

function rowOf(text: string) {
  // The first match inside the table: a name is also an option of the "who" box.
  const row = screen
    .getAllByText(text)
    .map((element) => element.closest("tr"))
    .find((found) => found !== null);
  if (row === undefined) {
    throw new Error(`No row shows ${text}`);
  }
  return row;
}

function lastQuery(api: ReturnType<typeof mockApi>) {
  const requests = api.requestsTo("GET", "/api/audit-logs");
  return new URL(requests[requests.length - 1]!.url).searchParams;
}

describe("AuditLogsPage", () => {
  it("AuditLogsPage_NothingChosen_OpensOnTodayWithoutSignIns", async () => {
    const api = ownerWith();

    renderApp("/audit-logs", { session: session() });

    expect(await screen.findByText("تغییرهای امروز")).toBeInTheDocument();
    const query = lastQuery(api);
    expect(query.get("From")).toBe(gymToday());
    expect(query.get("To")).toBeNull();
    expect(query.get("IncludeSignIns")).toBeNull();
  });

  it("AuditLogsPage_Rows_ShowWhoWhatAndTheMember", async () => {
    ownerWith();

    renderApp("/audit-logs", { session: session() });

    await screen.findByText("رضا احمدی", { selector: "td p" });
    const row = rowOf("رضا احمدی");
    expect(within(row).getByText("ویرایش")).toBeInTheDocument();
    expect(within(row).getByText("اشتراک")).toBeInTheDocument();
    expect(within(row).getByText("10.0.0.5")).toBeInTheDocument();
    expect(within(row).getByRole("button", { name: "تغییرهای سارا محمدی" })).toBeInTheDocument();
    expect(within(rowOf("کمد")).getByText("سیستم")).toBeInTheDocument();
  });

  it("AuditLogsPage_UpdateDetails_ShowEachFieldBeforeAndAfter", async () => {
    ownerWith();
    renderApp("/audit-logs", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "جزئیات ویرایش اشتراک" }));

    const fields = screen.getByRole("table", { name: "فیلدهای ویرایش اشتراک" });
    expect(within(fields).getByText("قبل")).toBeInTheDocument();
    const field = within(fields).getByText("جلسات استفاده‌شده").closest("tr")!;
    expect(within(field).getByText("۴")).toBeInTheDocument();
    expect(within(field).getByText("۵")).toBeInTheDocument();
  });

  it("AuditLogsPage_InsertDetails_FormatMoneyKindsAndUsersAndHideTheId", async () => {
    ownerWith();
    renderApp("/audit-logs", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "جزئیات ثبت پرداخت" }));

    const fields = screen.getByRole("table", { name: "فیلدهای ثبت پرداخت" });
    expect(within(fields).getByText("۱۸۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(within(fields).getByText("کارت")).toBeInTheDocument();
    expect(within(fields).getByText("مدیر باشگاه")).toBeInTheDocument();
    expect(within(fields).queryByText(/01990000/)).not.toBeInTheDocument();
    expect(within(fields).queryByText("قبل")).not.toBeInTheDocument();
  });

  it("AuditLogsPage_MemberClicked_ShowsTheirWholeHistory", async () => {
    const api = ownerWith({
      [`GET /api/members/${saraId}`]: () => json(200, { id: saraId, fullName: "سارا محمدی" }),
    });
    renderApp("/audit-logs", { session: session() });

    fireEvent.click((await screen.findAllByRole("button", { name: "تغییرهای سارا محمدی" }))[0]!);

    await waitFor(() => expect(lastQuery(api).get("MemberId")).toBe(saraId));
    expect(lastQuery(api).get("From")).toBeNull();
    expect(await screen.findByText("تغییرها")).toBeInTheDocument();
  });

  it("AuditLogsPage_RecordHistory_FiltersByKindAndId", async () => {
    const api = ownerWith();
    renderApp("/audit-logs", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "جزئیات ویرایش اشتراک" }));
    fireEvent.click(screen.getByRole("button", { name: "همهٔ تغییرهای این اشتراک" }));

    expect(await screen.findByText(/فقط تغییرهای یک اشتراک/)).toBeInTheDocument();
    await waitFor(() => expect(lastQuery(api).get("EntityId")).toBe(usedSession.entityId));
    const query = lastQuery(api);
    expect(query.get("EntityType")).toBe("Subscription");
    expect(query.get("EntityId")).toBe(usedSession.entityId);
    expect(query.get("From")).toBeNull();
  });

  it("AuditLogsPage_SystemChosen_AsksForTheRowsWithNoUser", async () => {
    const api = ownerWith();
    renderApp("/audit-logs", { session: session() });
    await screen.findByText("رضا احمدی", { selector: "td p" });

    fireEvent.change(screen.getByLabelText("کاربر"), { target: { value: "system" } });

    await waitFor(() => expect(lastQuery(api).get("SystemOnly")).toBe("true"));
    expect(lastQuery(api).get("UserId")).toBeNull();
  });

  it("AuditLogsPage_UserChosen_AsksForThatUser", async () => {
    const api = ownerWith();
    renderApp("/audit-logs", { session: session() });
    await screen.findByRole("option", { name: "رضا احمدی" });

    fireEvent.change(screen.getByLabelText("کاربر"), { target: { value: staffAccount.id } });

    await waitFor(() => expect(lastQuery(api).get("UserId")).toBe(staffAccount.id));
  });

  it("AuditLogsPage_SignInSwitch_AsksForThemAndOffersTheirKinds", async () => {
    const api = ownerWith();
    renderApp("/audit-logs", { session: session() });
    await screen.findByText("رضا احمدی", { selector: "td p" });
    expect(screen.queryByRole("option", { name: "نشست ورود" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByLabelText("نمایش ورودها و دستگاه‌ها"));

    expect(await screen.findByRole("option", { name: "نشست ورود" })).toBeInTheDocument();
    await waitFor(() => expect(lastQuery(api).get("IncludeSignIns")).toBe("true"));
  });

  it("AuditLogsPage_KindAndAction_AreSent", async () => {
    const api = ownerWith();
    renderApp("/audit-logs", { session: session() });
    await screen.findByText("رضا احمدی", { selector: "td p" });

    fireEvent.change(screen.getByLabelText("نوع رکورد"), { target: { value: "Payment" } });
    fireEvent.change(screen.getByLabelText("نوع تغییر"), { target: { value: "Insert" } });

    await waitFor(() => expect(lastQuery(api).get("Action")).toBe("Insert"));
    const query = lastQuery(api);
    expect(query.get("EntityType")).toBe("Payment");
    expect(query.get("From")).toBe(gymToday());
  });

  it("AuditLogsPage_BackwardsRange_SaysSoWithoutAsking", async () => {
    const api = ownerWith();

    renderApp("/audit-logs?from=2026-10-10&to=2026-10-01", { session: session() });

    expect(await screen.findByText("بازهٔ تاریخ نامعتبر است.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/audit-logs")).toHaveLength(0);
  });

  it("AuditLogsPage_NothingFound_SaysSo", async () => {
    ownerWith({ "GET /api/audit-logs": () => logsPage([]) });

    renderApp("/audit-logs", { session: session() });

    expect(await screen.findByText("تغییری با این فیلتر نیست.")).toBeInTheDocument();
  });

  it("AuditLogsPage_StaffUser_SeesNoAccessMessage", async () => {
    // BUSINESS_RULES.md §1: the audit log is the Owner's.
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/audit-logs", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/audit-logs")).toHaveLength(0);
  });

  it("AuditLogsPage_Owner_HasAMenuItem", async () => {
    mockApi(signedInHandlers(owner));

    renderApp("/", { session: session() });

    expect(await screen.findByRole("link", { name: "گزارش تغییرات" })).toHaveAttribute(
      "href",
      "/audit-logs",
    );
  });
});
