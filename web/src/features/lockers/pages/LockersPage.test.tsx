import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import { json, mockApi, owner, problem, session, signedInHandlers, staffUser } from "@/test/mockApi";
import { freeLocker, lockersPage, occupiedLocker, outOfServiceLocker } from "@/test/lockers";
import { renderApp } from "@/test/renderApp";

describe("LockersPage", () => {
  it("LockersPage_StaffUser_SeesNoAccessMessageAndNoRequest", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/lockers", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/lockers")).toHaveLength(0);
  });

  it("LockersPage_Owner_ListsLockersWithOccupancy", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/lockers": () => lockersPage([freeLocker, occupiedLocker, outOfServiceLocker]),
    });

    renderApp("/lockers", { session: session() });

    const freeRow = (await screen.findByText("۱")).closest("tr")!;
    expect(within(freeRow).getByText("آزاد")).toBeInTheDocument();

    const occupiedRow = screen.getByText("۲").closest("tr")!;
    expect(within(occupiedRow).getByText("اشغال")).toBeInTheDocument();

    const outOfServiceRow = screen.getByText("۳").closest("tr")!;
    expect(within(outOfServiceRow).getByText("خارج از سرویس")).toBeInTheDocument();
    expect(
      within(outOfServiceRow).getByRole("button", { name: "بازگرداندن به سرویس" }),
    ).toBeInTheDocument();
  });

  it("LockersPage_NoLockers_SaysSo", async () => {
    mockApi({ ...signedInHandlers(owner), "GET /api/lockers": () => lockersPage([]) });

    renderApp("/lockers", { session: session() });

    expect(await screen.findByText("هنوز هیچ کمدی تعریف نشده است.")).toBeInTheDocument();
  });

  it("LockersPage_AddLocker_CallsTheApiAndRefreshesTheList", async () => {
    let current = [freeLocker];
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/lockers": () => lockersPage(current),
      "POST /api/lockers": () => {
        const created = { ...freeLocker, id: "0199a000-0000-7000-8000-0000000000f9", number: 4 };
        current = [...current, created];
        return json(201, created);
      },
    });

    renderApp("/lockers", { session: session() });
    await screen.findByText("۱");

    fireEvent.change(screen.getByLabelText("شماره کمد"), { target: { value: "۴" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن" }));

    expect(await screen.findByRole("status")).toHaveTextContent("کمد شماره ۴ اضافه شد.");
    expect(api.requestsTo("POST", "/api/lockers")).toHaveLength(1);
    const body = await api.requestsTo("POST", "/api/lockers")[0]!.clone().json();
    expect(body).toEqual({ number: 4 });
  });

  it("LockersPage_AddLockerWithInvalidNumber_ShowsAMessageWithoutCallingTheApi", async () => {
    const api = mockApi({ ...signedInHandlers(owner), "GET /api/lockers": () => lockersPage([]) });

    renderApp("/lockers", { session: session() });
    await screen.findByText("هنوز هیچ کمدی تعریف نشده است.");

    fireEvent.change(screen.getByLabelText("شماره کمد"), { target: { value: "abc" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("شماره کمد باید عددی مثبت باشد.");
    expect(api.requestsTo("POST", "/api/lockers")).toHaveLength(0);
  });

  it("LockersPage_AddDuplicateNumber_ShowsThePersianReason", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/lockers": () => lockersPage([freeLocker]),
      "POST /api/lockers": () => problem(409, "Lockers.NumberAlreadyExists"),
    });

    renderApp("/lockers", { session: session() });
    await screen.findByText("۱");

    fireEvent.change(screen.getByLabelText("شماره کمد"), { target: { value: "1" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("کمد دیگری با همین شماره وجود دارد.");
  });

  it("LockersPage_ToggleOutOfService_CallsTheApi", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/lockers": () => lockersPage([freeLocker]),
      [`POST /api/lockers/${freeLocker.id}/out-of-service`]: () =>
        json(200, { ...freeLocker, isOutOfService: true, version: 2 }),
    });

    renderApp("/lockers", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "خارج از سرویس" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/lockers/${freeLocker.id}/out-of-service`)).toHaveLength(1),
    );
    expect(await screen.findByText("خارج از سرویس")).toBeInTheDocument();
  });

  it("LockersPage_ToggleOccupiedOutOfService_ShowsThePersianReason", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/lockers": () => lockersPage([occupiedLocker]),
      [`POST /api/lockers/${occupiedLocker.id}/out-of-service`]: () =>
        problem(422, "Lockers.Occupied"),
    });

    renderApp("/lockers", { session: session() });

    fireEvent.click(await screen.findByRole("button", { name: "خارج از سرویس" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "این کمد اشغال است و نمی‌توان آن را از سرویس خارج کرد.",
    );
  });
});
