import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { CafeOrder } from "@/features/cafe/api";
import {
  cardioCharge,
  closedVisit,
  currentlyInsidePage,
  guestInsideRow,
  guestVisit,
  todayByHour,
} from "@/test/attendance";
import { cafePage, orderOnAccount } from "@/test/cafe";
import {
  allLockers,
  guestLocker,
  locker,
  lockerId,
  lockersPage,
  lockerVisit,
} from "@/test/lockers";
import {
  json,
  mockApi,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

// A guest's visit on the desk's map (BUSINESS_RULES.md §7 *Guest visit*), as Staff: letting a
// relative in by name, their door in its own colour, and their box, where the cafe is settled
// before they can leave.

const justNow = new Date().toISOString();

/** Maryam, a guest, on locker 3. */
const maryamVisit = { ...guestVisit("مریم احمدی"), lockerId: lockerId(3), checkedInAt: justNow };

/** A drink Maryam picked up and has not paid for. */
const unpaidDrink: CafeOrder = {
  ...orderOnAccount,
  id: "0199a000-0000-7000-8000-0000000009f1",
  memberId: null,
  memberFullName: null,
  guestName: "مریم احمدی",
  attendanceId: maryamVisit.id,
  totalAmount: 40000,
  netPaid: 0,
  outstanding: 40000,
  paymentStatus: "Unpaid",
};

function mapHandlers(
  lockers = allLockers(),
  inside: ReturnType<typeof guestInsideRow>[] = [],
  extra: Record<string, Handler> = {},
): Record<string, Handler> {
  return {
    ...signedInHandlers(staffUser),
    "GET /api/lockers": () => lockersPage(lockers),
    "GET /api/attendance/currently-inside": () => currentlyInsidePage(inside),
    "GET /api/attendance/today-by-hour": () => todayByHour(),
    "GET /api/cafe/orders": () => cafePage([]),
    ...extra,
  };
}

function renderMap() {
  return renderApp("/", { session: session() });
}

/** A locker's door on the map, by its number. The comma keeps "کمد ۲،" from matching "کمد ۲۰،". */
async function door(number: string) {
  return screen.findByRole("button", { name: new RegExp(`^کمد ${number}،`) });
}

describe("Guest visit", () => {
  // ---- Letting a guest in ----

  it("GuestCheckIn_FromAFreeLocker_SendsOnlyTheNameAndShowsTheLocker", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "POST /api/attendance/guest-check-in": () =>
          json(201, { ...maryamVisit, lockerId: lockerId(12), lockerNumber: 12 }),
      }),
    );
    renderMap();

    fireEvent.click(await door("۱۲"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "ورود مهمان" }));
    expect(
      within(dialog).getByRole("heading", { name: "کمد شماره ۱۲ — ورود مهمان" }),
    ).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی مهمان"), {
      target: { value: "  مریم احمدی " },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت ورود مهمان" }));

    expect(await within(dialog).findByText("ورود مهمان ثبت شد")).toBeInTheDocument();
    expect(dialog).toHaveTextContent("مریم احمدی · مهمان");
    expect(within(dialog).getByLabelText("کمد شماره ۱۲")).toBeInTheDocument();
    const requests = api.requestsTo("POST", "/api/attendance/guest-check-in");
    expect(requests).toHaveLength(1);
    expect(await requests[0]!.clone().json()).toEqual({
      guestName: "مریم احمدی",
      lockerId: lockerId(12),
    });
  });

  it("GuestCheckIn_BlankName_SaysSoAndSendsNothing", async () => {
    const api = mockApi(mapHandlers());
    renderMap();

    fireEvent.click(await door("۱۲"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "ورود مهمان" }));
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی مهمان"), {
      target: { value: "   " },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت ورود مهمان" }));

    expect(
      await within(dialog).findByText("نام و نام خانوادگی مهمان را وارد کنید."),
    ).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/attendance/guest-check-in")).toHaveLength(0);
  });

  it("GuestCheckIn_LockerTakenMeanwhile_ShowsWhyAboveTheForm", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "POST /api/attendance/guest-check-in": () => problem(409, "Attendance.LockerTaken"),
      }),
    );
    renderMap();

    fireEvent.click(await door("۱۲"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "ورود مهمان" }));
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی مهمان"), {
      target: { value: "مریم احمدی" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت ورود مهمان" }));

    expect(await within(dialog).findByText(/این کمد را کس دیگری گرفته است/)).toBeInTheDocument();
  });

  it("GuestCheckIn_FromAReservePlace_SendsNoLocker", async () => {
    const full = Array.from({ length: 72 }, (_, index) =>
      locker(index + 1, { isOutOfService: true }),
    );
    const api = mockApi(
      mapHandlers(full, [], {
        "POST /api/attendance/guest-check-in": () =>
          json(201, { ...maryamVisit, lockerId: null, lockerNumber: null, usesReservePlace: true }),
      }),
    );
    renderMap();

    fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));
    fireEvent.click(screen.getAllByRole("button", { name: "جای خالی ورود بدون کمد" })[0]!);
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "ورود مهمان" }));
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی مهمان"), {
      target: { value: "مریم احمدی" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت ورود مهمان" }));

    expect(await within(dialog).findByText("ورود مهمان ثبت شد")).toBeInTheDocument();
    const requests = api.requestsTo("POST", "/api/attendance/guest-check-in");
    expect(await requests[0]!.clone().json()).toEqual({ guestName: "مریم احمدی", lockerId: null });
  });

  // ---- On the map ----

  it("Map_GuestHoldsALocker_DrawsItInTheGuestColourWithTheirNameAndALegendLine", async () => {
    mockApi(mapHandlers(allLockers(guestLocker(3, "مریم احمدی")), [guestInsideRow(maryamVisit)]));
    renderMap();

    const held = await door("۳");
    expect(held).toHaveAttribute("data-state", "guest");
    expect(held).toHaveTextContent("مریم احمدی");
    expect(held).toHaveAccessibleName("کمد ۳، مهمان — مریم احمدی");
    expect(held).not.toHaveTextContent("بدهکار");

    const legend = screen.getByRole("list", { name: "راهنمای کمدها" });
    expect(legend).toHaveTextContent("۱ مهمان");
    expect(legend).toHaveTextContent("۷۱ آزاد");
  });

  it("Map_GuestWithUnpaidCafe_CarriesTheDebtorTag", async () => {
    mockApi(
      mapHandlers(allLockers(guestLocker(3, "مریم احمدی", 40000)), [
        guestInsideRow(maryamVisit, [unpaidDrink]),
      ]),
    );
    renderMap();

    const held = await door("۳");
    expect(held).toHaveTextContent("بدهکار");
    expect(held).toHaveAccessibleName("کمد ۳، مهمان — مریم احمدی، بدهکار");
  });

  it("ReservePlaces_GuestOnOne_ShowsTheNameMarkedAsAGuest", async () => {
    const onReserve = {
      ...maryamVisit,
      lockerId: null,
      lockerNumber: null,
      usesReservePlace: true,
    };
    mockApi(mapHandlers(allLockers(), [guestInsideRow(onReserve)]));
    renderMap();

    fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));

    expect(screen.getByRole("button", { name: "مریم احمدی، مهمان" })).toBeInTheDocument();
  });

  // ---- The guest's box ----

  it("GuestBox_Opened_ShowsTheNameAndTheFourTilesWithNoProfileOrSessions", async () => {
    mockApi(mapHandlers(allLockers(guestLocker(3, "مریم احمدی")), [guestInsideRow(maryamVisit)]));
    renderMap();

    fireEvent.click(await door("۳"));
    const dialog = await screen.findByRole("dialog");

    expect(within(dialog).getByRole("heading", { name: "کمد شماره ۳" })).toBeInTheDocument();
    expect(dialog).toHaveTextContent("مریم احمدی");
    expect(dialog).toHaveTextContent("مهمان");
    expect(within(dialog).queryByRole("link")).not.toBeInTheDocument();
    expect(within(dialog).queryByText("جلسات")).not.toBeInTheDocument();
    // A guest may use every service (task 6.5.31): the same four tiles as a member's box.
    for (const tile of ["هوازی", "بوفه", "فروشگاه", "آنالیز"]) {
      expect(within(dialog).getByText(tile)).toBeInTheDocument();
    }
    // Nothing bought, nothing owed: the guest can leave.
    expect(within(dialog).queryByRole("button", { name: /تسویه یکجا/ })).not.toBeInTheDocument();
    for (const action of ["ثبت خروج", "لغو ورود", "جابه‌جایی کمد"]) {
      expect(within(dialog).getByRole("button", { name: action })).toBeEnabled();
    }
  });

  it("GuestBox_UnpaidCafe_PaysEverythingAtOnceThenLetsThemCheckOut", async () => {
    let settled = false;
    const api = mockApi(
      mapHandlers(allLockers(guestLocker(3, "مریم احمدی", 40000)), [], {
        "GET /api/attendance/currently-inside": () =>
          currentlyInsidePage([
            guestInsideRow(maryamVisit, [
              settled
                ? { ...unpaidDrink, netPaid: 40000, outstanding: 0, paymentStatus: "Paid" }
                : unpaidDrink,
            ]),
          ]),
        [`POST /api/attendance/${maryamVisit.id}/settle-guest`]: () => {
          settled = true;
          return json(200, { amount: 40000, method: "Cash", payments: [], remainingDebt: 0 });
        },
        "GET /api/cafe/orders": () => cafePage([]),
        [`POST /api/attendance/${maryamVisit.id}/check-out`]: () =>
          json(200, { ...closedVisit(""), ...maryamVisit, checkedOutAt: justNow }),
      }),
    );
    renderMap();

    fireEvent.click(await door("۳"));
    const dialog = await screen.findByRole("dialog");
    // A guest has no account to leave the drink on (BUSINESS_RULES.md §7 *Guest visit*).
    expect(within(dialog).getByRole("button", { name: "ثبت خروج" })).toBeDisabled();
    expect(dialog).toHaveTextContent("خروج پس از پرداخت همهٔ خریدها ثبت می‌شود.");

    fireEvent.click(within(dialog).getByRole("button", { name: /تسویه یکجا/ }));
    const form = within(dialog).getByRole("form", { name: "تسویه یکجا" });
    expect(form).toHaveTextContent("۴۰٬۰۰۰");
    fireEvent.change(within(form).getByLabelText("روش پرداخت"), { target: { value: "Cash" } });
    fireEvent.click(within(form).getByRole("button", { name: "تأیید پرداخت" }));
    const confirm = await screen.findByRole("dialog", { name: "آیا پول دریافت شد؟" });
    fireEvent.click(within(confirm).getByRole("button", { name: "بله، پول دریافت شد" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "ثبت خروج" })).toBeEnabled());
    const settles = api.requestsTo("POST", `/api/attendance/${maryamVisit.id}/settle-guest`);
    expect(settles).toHaveLength(1);
    expect(await settles[0]!.clone().json()).toEqual({
      amount: "40000.00",
      method: "Cash",
      referenceNumber: null,
    });

    fireEvent.click(screen.getByRole("button", { name: "ثبت خروج" }));
    const checkOut = await screen.findByRole("dialog");
    expect(checkOut).toHaveTextContent("آیا از ثبت خروج مریم احمدی مطمئن هستید؟");
    fireEvent.click(within(checkOut).getByLabelText("کلید کمد شماره ۳ را تحویل گرفتم"));
    expect(checkOut).toHaveTextContent("کلید کمد را از مهمان تحویل بگیرید");
    fireEvent.click(within(checkOut).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(checkOut).findByText("کمد شماره ۳ آزاد شد.")).toBeInTheDocument();
  });

  it("GuestBox_UnpaidCardio_BlocksCheckOutAndTheSettlePaysItWithTheCafe", async () => {
    const withCardio = {
      ...maryamVisit,
      serviceCharges: [cardioCharge(maryamVisit, { amount: 30000 })],
    };
    const api = mockApi(
      mapHandlers(
        allLockers(guestLocker(3, "مریم احمدی", 70000)),
        [guestInsideRow(withCardio, [unpaidDrink])],
        {
          [`POST /api/attendance/${maryamVisit.id}/settle-guest`]: () =>
            json(200, { amount: 70000, method: "Cash", payments: [], remainingDebt: 0 }),
        },
      ),
    );
    renderMap();

    fireEvent.click(await door("۳"));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "ثبت خروج" })).toBeDisabled();
    expect(within(dialog).getByRole("button", { name: /^هوازی/ })).toHaveTextContent("۳۰٬۰۰۰");

    fireEvent.click(within(dialog).getByRole("button", { name: /تسویه یکجا/ }));
    const form = within(dialog).getByRole("form", { name: "تسویه یکجا" });
    // 40,000 for the drink and 30,000 for the treadmill.
    expect(form).toHaveTextContent("۷۰٬۰۰۰");
    fireEvent.change(within(form).getByLabelText("روش پرداخت"), { target: { value: "Cash" } });
    fireEvent.click(within(form).getByRole("button", { name: "تأیید پرداخت" }));
    const confirm = await screen.findByRole("dialog", { name: "آیا پول دریافت شد؟" });
    fireEvent.click(within(confirm).getByRole("button", { name: "بله، پول دریافت شد" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/attendance/${maryamVisit.id}/settle-guest`)).toHaveLength(
        1,
      ),
    );
    const request = api.requestsTo("POST", `/api/attendance/${maryamVisit.id}/settle-guest`)[0]!;
    expect(await request.clone().json()).toMatchObject({ amount: "70000.00" });
  });

  it("GuestBox_RecordingCardio_PostsItOnTheGuestsVisit", async () => {
    const api = mockApi(
      mapHandlers(allLockers(guestLocker(3, "مریم احمدی")), [guestInsideRow(maryamVisit)], {
        [`POST /api/attendance/${maryamVisit.id}/service-charges`]: () =>
          json(201, cardioCharge(maryamVisit, { amount: 30000 })),
      }),
    );
    renderMap();

    fireEvent.click(await door("۳"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "هوازی" }));
    fireEvent.change(screen.getByLabelText("مبلغ هوازی"), { target: { value: "30000" } });
    fireEvent.click(screen.getByRole("button", { name: "ثبت" }));

    await waitFor(() =>
      expect(
        api.requestsTo("POST", `/api/attendance/${maryamVisit.id}/service-charges`),
      ).toHaveLength(1),
    );
    const request = api.requestsTo("POST", `/api/attendance/${maryamVisit.id}/service-charges`)[0]!;
    expect(await request.clone().json()).toMatchObject({ kind: "Cardio" });
  });

  it("GuestBox_ShopTile_SaysTheSaleIsUnderTheGuestsNameNotAnAccount", async () => {
    mockApi(mapHandlers(allLockers(guestLocker(3, "مریم احمدی")), [guestInsideRow(maryamVisit)]));
    renderMap();

    fireEvent.click(await door("۳"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "فروشگاه" }));

    const shop = await screen.findByRole("dialog", { name: /فروشگاه — مریم احمدی/ });
    expect(shop).toHaveTextContent("به نام مهمان ثبت می‌شود و پیش از خروج پرداخت می‌شود.");
    expect(shop).not.toHaveTextContent("حساب عضو");
  });

  it("TodayHistory_GuestHadTheLocker_IsListedByNameAsAGuestWithNoLink", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        [`GET /api/lockers/${lockerId(12)}/today`]: () =>
          json(200, [
            lockerVisit({ memberId: null, memberFullName: null, guestName: "مریم احمدی" }),
          ]),
      }),
    );
    renderMap();

    fireEvent.click(await door("۱۲"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "تاریخچه امروز این کمد" }));

    const list = await within(dialog).findByRole("list", { name: "تاریخچه امروز کمد" });
    expect(list).toHaveTextContent("مریم احمدی");
    expect(list).toHaveTextContent("مهمان");
    expect(within(list).queryByRole("link")).not.toBeInTheDocument();
  });
});
