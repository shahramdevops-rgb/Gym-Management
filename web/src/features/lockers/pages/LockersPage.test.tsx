import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Member } from "@/features/members/api";
import { cafePage } from "@/test/cafe";
import { closedVisit, currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { clickOutsideDialog } from "@/test/dialog";
import { allLockers, heldLocker, locker, lockerId, lockersPage, lockerVisit } from "@/test/lockers";
import {
  json,
  mockApi,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import {
  ali,
  debtItem,
  memberDebt,
  membersPage,
  reza,
  serviceChargeDebtItem,
} from "@/test/members";
import { renderApp } from "@/test/renderApp";
import { pricesResponse } from "@/test/prices";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

// Every test signs in as Staff: the map is the desk's screen, the same for both roles, and nothing
// on it is Owner-only (BUSINESS_RULES.md §6). A test passing here is a test that Staff can do it.

/** Reza's open visit on locker 2, as the "currently inside" list carries it. */
const rezaVisit = { ...openVisit(reza.id), lockerId: lockerId(2), lockerNumber: 2 };

function mapHandlers(
  lockers = allLockers(),
  inside: ReturnType<typeof insideRow>[] = [],
  extra: Record<string, Handler> = {},
): Record<string, Handler> {
  return {
    ...signedInHandlers(staffUser),
    "GET /api/lockers": () => lockersPage(lockers),
    "GET /api/attendance/currently-inside": () => currentlyInsidePage(inside),
    [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([activeSubscription]),
    [`GET /api/members/${reza.id}/debt`]: () => memberDebt([]),
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

async function openFreeLocker(number: string) {
  fireEvent.click(await door(number));
  return screen.findByRole("dialog");
}

async function search(dialog: HTMLElement, text: string) {
  fireEvent.change(within(dialog).getByRole("searchbox", { name: "نام یا شماره موبایل" }), {
    target: { value: text },
  });
}

async function chooseAndConfirm(dialog: HTMLElement, name: string) {
  await search(dialog, "رضا");
  fireEvent.click(await within(dialog).findByRole("button", { name: new RegExp(name) }));
  fireEvent.click(await within(dialog).findByRole("button", { name: "بله، ورود ثبت شود" }));
}

describe("LockersPage", () => {
  // ---- The map ----

  it("Map_Loaded_DrawsTwoZonesAnd72DoorsWithStatesAndCounts", async () => {
    mockApi(
      mapHandlers(
        allLockers(heldLocker(2, reza.id, reza.fullName), locker(3, { isOutOfService: true })),
        [insideRow(reza.fullName, rezaVisit)],
      ),
    );
    renderMap();

    expect(await screen.findByRole("heading", { name: "ورود با کمد" })).toBeInTheDocument();
    const outside = await screen.findByRole("region", { name: "بیرون رختکن" });
    const inside = screen.getByRole("region", { name: "داخل رختکن" });
    expect(within(outside).getAllByRole("button", { name: /^کمد / })).toHaveLength(30);
    expect(within(inside).getAllByRole("button", { name: /^کمد / })).toHaveLength(42);
    // The zone names are for a screen reader only; the drawing says where a locker is.
    expect(screen.queryByText("بیرون رختکن")).not.toBeInTheDocument();
    expect(screen.queryByText("داخل رختکن")).not.toBeInTheDocument();

    const free = await door("۱");
    expect(free).toHaveAttribute("data-state", "free");
    expect(free).toHaveTextContent(/^۱$/);
    const held = await door("۲");
    expect(held).toHaveAttribute("data-state", "occupied");
    // The holder's name, written on the door, on hover, and for a screen reader.
    expect(held).toHaveTextContent("رضا احمدی");
    expect(held).toHaveAttribute("title", "رضا احمدی");
    expect(held).toHaveAccessibleName("کمد ۲، اشغال — رضا احمدی");
    expect(await door("۳")).toHaveAttribute("data-state", "outOfService");

    const legend = screen.getByRole("list", { name: "راهنمای کمدها" });
    expect(legend).toHaveTextContent("آزاد: ۷۰");
    expect(legend).toHaveTextContent("اشغال: ۱");
    expect(legend).toHaveTextContent("خارج از سرویس: ۱");
  });

  it("Map_HolderOwesMoney_MarksTheirDoorDebtorAndNoOtherDoor", async () => {
    mockApi(
      mapHandlers(
        allLockers(
          heldLocker(2, reza.id, reza.fullName),
          heldLocker(67, ali.id, ali.fullName, 150_000),
        ),
      ),
    );
    renderMap();

    const owing = await door("۶۷");
    // The label, on hover and for a screen reader, besides the name (BUSINESS_RULES.md §6).
    expect(owing).toHaveTextContent("بدهکار");
    expect(owing).toHaveTextContent(ali.fullName);
    expect(owing).toHaveAttribute("title", `${ali.fullName}، بدهکار`);
    expect(owing).toHaveAccessibleName(`کمد ۶۷، اشغال — ${ali.fullName}، بدهکار`);

    // A holder who owes nothing, and a free door, carry no label.
    expect(await door("۲")).not.toHaveTextContent("بدهکار");
    expect(screen.getAllByText("بدهکار")).toHaveLength(1);
  });

  it("Map_Cabinets_RunDownAColumnThenOnToTheNext", async () => {
    mockApi(mapHandlers());
    renderMap();

    const cabinet = await screen.findByTestId("cabinet-1");
    const numbers = within(cabinet)
      .getAllByRole("button")
      .map((button) => button.textContent);

    expect(numbers).toEqual(["۱", "۲", "۳", "۴", "۵", "۶"]);
    // Left to right like the wall, though the page is right-to-left.
    expect(cabinet.closest("[dir='ltr']")).not.toBeNull();
  });

  // ---- A free locker: check-in ----

  it("FreeLocker_Clicked_OpensTheSearchForThatLocker", async () => {
    mockApi(mapHandlers());
    renderMap();

    const dialog = await openFreeLocker("۱۲");

    expect(within(dialog).getByRole("heading", { name: "کمد شماره ۱۲" })).toBeInTheDocument();
    expect(within(dialog).getByRole("searchbox", { name: "نام یا شماره موبایل" })).toHaveFocus();
  });

  it("FreeLocker_ClickedOutsideTheBox_ClosesIt", async () => {
    mockApi(mapHandlers());
    renderMap();

    await openFreeLocker("۱۲");
    await clickOutsideDialog();

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("Search_MemberAlreadyInside_IsMarkedAndChoosingThemSendsNothing", async () => {
    const rezaInside: Member = {
      ...reza,
      currentVisit: {
        attendanceId: rezaVisit.id,
        lockerNumber: 5,
        usesReservePlace: false,
        checkedInAt: rezaVisit.checkedInAt,
      },
    };
    const api = mockApi(
      mapHandlers(allLockers(), [], { "GET /api/members": () => membersPage([rezaInside]) }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await search(dialog, "رضا");
    const found = await within(dialog).findByRole("button", { name: /رضا احمدی/ });
    expect(found).toHaveTextContent("داخل باشگاه — کمد ۵");

    fireEvent.click(found);

    expect(within(dialog).getByRole("alert")).toHaveTextContent(
      "رضا احمدی هم‌اکنون داخل باشگاه است (کمد ۵).",
    );
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("CheckIn_Confirmed_SendsTheChosenLockerAndShowsItWithThePlanAndDebt", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          json(201, { ...openVisit(reza.id), lockerId: lockerId(12), lockerNumber: 12 }),
        [`GET /api/members/${reza.id}/debt`]: () =>
          memberDebt([
            debtItem({ outstanding: 2000000 }),
            serviceChargeDebtItem({ outstanding: 50000 }),
          ]),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await search(dialog, "رضا");
    fireEvent.click(await within(dialog).findByRole("button", { name: /رضا احمدی/ }));
    await waitFor(() =>
      expect(dialog).toHaveTextContent("آیا از ثبت ورود رضا احمدی با کمد شماره ۱۲ مطمئن هستید؟"),
    );
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود ثبت شود" }));

    expect(await within(dialog).findByText("ورود ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("کمد شماره ۱۲")).toBeInTheDocument();
    expect(await within(dialog).findByText("۳۰ روز · ۱۲ جلسه")).toBeInTheDocument();
    // Nothing was frozen, so nothing is said about it.
    expect(within(dialog).queryByText(/فریز/)).not.toBeInTheDocument();
    expect(await within(dialog).findByRole("region", { name: "بدهی" })).toHaveTextContent("هوازی");

    const requests = api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`);
    expect(requests).toHaveLength(1);
    expect(await requests[0]!.clone().json()).toEqual({ lockerId: lockerId(12) });
  });

  // ---- A free locker: who had it today ----

  it("TodayHistory_BeforeChoosingAMember_ListsTodaysHoldersLinkedToTheirProfiles", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        [`GET /api/lockers/${lockerId(12)}/today`]: () =>
          json(200, [
            lockerVisit({ memberId: reza.id, memberFullName: reza.fullName }),
            lockerVisit({
              attendanceId: "0199b000-0000-7000-8000-000000000002",
              memberId: ali.id,
              memberFullName: ali.fullName,
              checkedInAt: "2026-09-28T08:00:00Z",
              checkedOutAt: "2026-09-28T08:05:00Z",
              cancelledAt: "2026-09-28T08:05:00Z",
            }),
          ]),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    // Nothing is fetched until the desk asks.
    expect(api.requestsTo("GET", `/api/lockers/${lockerId(12)}/today`)).toHaveLength(0);
    fireEvent.click(within(dialog).getByRole("button", { name: "تاریخچه امروز این کمد" }));

    const list = await within(dialog).findByRole("list", { name: "تاریخچه امروز کمد" });
    const rows = within(list).getAllByRole("listitem");
    expect(rows).toHaveLength(2);
    expect(within(rows[0]!).getByRole("link", { name: "رضا احمدی" })).toHaveAttribute(
      "href",
      `/members/${reza.id}`,
    );
    expect(rows[0]).toHaveTextContent("ورود ۰۹:۰۰ · خروج ۱۰:۳۰");
    expect(rows[0]).not.toHaveTextContent("لغو شده");
    expect(within(rows[1]!).getByRole("link", { name: ali.fullName })).toHaveAttribute(
      "href",
      `/members/${ali.id}`,
    );
    expect(rows[1]).toHaveTextContent("لغو شده");

    fireEvent.click(within(dialog).getByRole("button", { name: "بازگشت" }));
    expect(
      await within(dialog).findByRole("searchbox", { name: "نام یا شماره موبایل" }),
    ).toBeInTheDocument();
  });

  it("TodayHistory_AtTheConfirmation_IsNotOffered", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await search(dialog, "رضا");
    fireEvent.click(await within(dialog).findByRole("button", { name: /رضا احمدی/ }));
    // Once a member is chosen, the box is only about confirming them.
    expect(await within(dialog).findByRole("button", { name: "بله، ورود ثبت شود" })).toBeEnabled();
    expect(within(dialog).getByRole("button", { name: "انصراف" })).toBeEnabled();
    expect(
      within(dialog).queryByRole("button", { name: "تاریخچه امروز این کمد" }),
    ).not.toBeInTheDocument();
  });

  it("TodayHistory_NobodyHadIt_SaysSo", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        [`GET /api/lockers/${lockerId(12)}/today`]: () => json(200, []),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    fireEvent.click(within(dialog).getByRole("button", { name: "تاریخچه امروز این کمد" }));

    expect(
      await within(dialog).findByText("امروز کسی از این کمد استفاده نکرده است."),
    ).toBeInTheDocument();
  });

  it("CheckIn_LockerTakenMeanwhile_ShowsWhyWithoutOfferingASingleVisit", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          problem(409, "Attendance.LockerTaken"),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await chooseAndConfirm(dialog, "رضا احمدی");

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "این کمد را کس دیگری گرفته است.",
    );
    expect(
      within(dialog).queryByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).not.toBeInTheDocument();
  });

  /** Check-in refuses until a request carries a sale, which the API sells and checks in at once. */
  function checkInSellsWith(refusal: string) {
    return async (request: Request) => {
      const body = (await request.clone().json()) as { sale?: unknown };
      return body.sale === undefined
        ? problem(422, refusal)
        : json(201, { ...openVisit(reza.id), lockerId: lockerId(12), lockerNumber: 12 });
    };
  }

  it("CheckIn_NoSubscription_SellsASingleVisitAndChecksInWithTheSameLockerInOneRequest", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        "GET /api/pricing": () => pricesResponse(),
        [`POST /api/members/${reza.id}/attendance/check-in`]: checkInSellsWith(
          "Attendance.NoSubscription",
        ),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await chooseAndConfirm(dialog, "رضا احمدی");
    fireEvent.click(await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }));

    expect(await within(dialog).findByText("ورود تک‌جلسه‌ای ثبت شد")).toBeInTheDocument();
    const checkIns = api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`);
    expect(checkIns).toHaveLength(2);
    expect(await checkIns[1]!.clone().json()).toEqual({
      lockerId: lockerId(12),
      sale: { kind: "SingleVisit" },
    });
    // The old two-step sale is gone: nothing is sold without its check-in.
    expect(
      api.requestsTo("POST", `/api/members/${reza.id}/subscriptions/single-visit`),
    ).toHaveLength(0);
  });

  it("CheckIn_NoSubscription_SellsAPlanRightHereAndChecksInWithTheSameLocker", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        "GET /api/pricing": () => pricesResponse(),
        [`POST /api/members/${reza.id}/attendance/check-in`]:
          checkInSellsWith("Subscriptions.Expired"),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await chooseAndConfirm(dialog, "رضا احمدی");
    fireEvent.click(await within(dialog).findByRole("button", { name: "فروش اشتراک" }));
    const sale = within(dialog).getByRole("region", { name: "فروش اشتراک" });
    fireEvent.change(within(sale).getByLabelText("تعداد روز"), { target: { value: "۳۰" } });
    fireEvent.change(within(sale).getByLabelText("تعداد جلسات"), { target: { value: "12" } });
    // The price before the desk confirms: 12 × 75,000.
    expect(await within(sale).findByText(/۹۰۰٬۰۰۰/)).toBeInTheDocument();
    fireEvent.click(within(sale).getByRole("button", { name: "فروش و ثبت ورود" }));

    expect(await within(dialog).findByText("اشتراک فروخته شد و ورود ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("کمد شماره ۱۲")).toBeInTheDocument();
    const checkIns = api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`);
    expect(checkIns).toHaveLength(2);
    expect(await checkIns[1]!.clone().json()).toEqual({
      lockerId: lockerId(12),
      sale: { kind: "Membership", durationDays: 30, sessionCount: 12 },
    });
    expect(api.requestsTo("POST", `/api/members/${reza.id}/subscriptions`)).toHaveLength(0);
  });

  it("CheckIn_PlanSaleRefused_ShowsWhyInTheFormAndStaysOpen", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        "GET /api/pricing": () => pricesResponse(),
        [`POST /api/members/${reza.id}/attendance/check-in`]: async (request) => {
          const body = (await request.clone().json()) as { sale?: unknown };
          return body.sale === undefined
            ? problem(422, "Attendance.NoSubscription")
            : problem(409, "Attendance.LockerTaken");
        },
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await chooseAndConfirm(dialog, "رضا احمدی");
    fireEvent.click(await within(dialog).findByRole("button", { name: "فروش اشتراک" }));
    const sale = within(dialog).getByRole("region", { name: "فروش اشتراک" });
    fireEvent.change(within(sale).getByLabelText("تعداد روز"), { target: { value: "30" } });
    fireEvent.change(within(sale).getByLabelText("تعداد جلسات"), { target: { value: "12" } });
    await within(sale).findByText(/۹۰۰٬۰۰۰/);
    fireEvent.click(within(sale).getByRole("button", { name: "فروش و ثبت ورود" }));

    expect(await within(sale).findByText(/این کمد را کس دیگری گرفته است/)).toBeInTheDocument();
    expect(within(dialog).queryByText("اشتراک فروخته شد و ورود ثبت شد")).not.toBeInTheDocument();
  });

  it("CheckIn_PlanBoughtForLater_OffersTheSingleVisitAndLeavesPlansToTheProfile", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        "GET /api/pricing": () => pricesResponse(),
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          problem(422, "Subscriptions.NotStarted"),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await chooseAndConfirm(dialog, "رضا احمدی");

    // A new plan would queue behind the one bought for later and could not let them in today.
    expect(
      await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: "فروش اشتراک" })).toHaveAttribute(
      "href",
      `/members/${reza.id}`,
    );
  });

  it("CheckIn_FrozenPlan_WarnsBeforeConfirmingAndSaysAfterwardsItWasUnfrozen", async () => {
    const frozen = { ...activeSubscription, status: "Frozen" as const, frozenSince: "2026-09-14" };
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        [`GET /api/members/${reza.id}/subscriptions`]: () => subscriptionsPage([frozen]),
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          json(201, {
            ...openVisit(reza.id),
            lockerId: lockerId(12),
            lockerNumber: 12,
            unfrozenDays: 4,
          }),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await search(dialog, "رضا");
    fireEvent.click(await within(dialog).findByRole("button", { name: /رضا احمدی/ }));

    // BUSINESS_RULES.md §4 Freeze: said before anything is sent, since coming in ends the freeze.
    expect(await within(dialog).findByText(/اشتراک او فریز است/)).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود ثبت شود" }));

    expect(await within(dialog).findByText("ورود ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByRole("status")).toHaveTextContent(
      "اشتراک فریز بود و با این ورود از حالت فریز خارج شد؛ ۴ روز به پایان آن اضافه شد.",
    );
  });

  it("CheckIn_SingleVisitHeldAlongAFrozenPlan_DoesNotWarn", async () => {
    const frozen = { ...activeSubscription, status: "Frozen" as const, frozenSince: "2026-09-14" };
    const singleVisit = {
      ...activeSubscription,
      id: "0199a000-0000-7000-8000-0000000000c9",
      isSingleSession: true,
      durationDays: 1,
      totalSessions: 1,
      usedSessions: 0,
      remainingSessions: 1,
      startDate: "2026-09-18",
      endDate: "2026-09-18",
    };
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        // Newest first, as the API sends them: the visit for today is what check-in will use.
        [`GET /api/members/${reza.id}/subscriptions`]: () =>
          subscriptionsPage([singleVisit, frozen]),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await search(dialog, "رضا");
    fireEvent.click(await within(dialog).findByRole("button", { name: /رضا احمدی/ }));

    await within(dialog).findByRole("button", { name: "بله، ورود ثبت شود" });
    await waitFor(() =>
      expect(api.requestsTo("GET", `/api/members/${reza.id}/subscriptions`)).not.toHaveLength(0),
    );
    // The freeze is left alone (§4), so there is nothing to warn about.
    expect(within(dialog).queryByText(/فریز/)).not.toBeInTheDocument();
  });

  it("CheckIn_NobodyFound_RegistersThemAndGoesStraightToTheSale", async () => {
    const created = { ...reza, fullName: "سارا محمدی" };
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([]),
        "GET /api/pricing": () => pricesResponse(),
        "POST /api/members": () => json(201, created),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await search(dialog, "سارا");
    fireEvent.click(await within(dialog).findByRole("button", { name: /ثبت این شخص/ }));
    expect(within(dialog).getByLabelText("نام و نام خانوادگی")).toHaveValue("سارا");
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی"), {
      target: { value: "سارا محمدی" },
    });
    fireEvent.change(within(dialog).getByLabelText("شماره موبایل"), {
      target: { value: "09121110000" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت و ادامه" }));

    // No check-in to confirm: someone registered a moment ago has nothing it could use.
    expect(await within(dialog).findByText("عضو جدید ثبت شد")).toBeInTheDocument();
    expect(
      await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "فروش اشتراک" })).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(1);
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("FreeLocker_TakeOutOfService_CallsTheApi", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        [`POST /api/lockers/${lockerId(9)}/out-of-service`]: () =>
          json(200, locker(9, { isOutOfService: true })),
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۹");
    fireEvent.click(within(dialog).getByRole("button", { name: "خارج از سرویس کردن این کمد" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/lockers/${lockerId(9)}/out-of-service`)).toHaveLength(1),
    );
  });

  it("OutOfServiceLocker_Clicked_OffersToBringItBack", async () => {
    const api = mockApi(
      mapHandlers(allLockers(locker(3, { isOutOfService: true })), [], {
        [`POST /api/lockers/${lockerId(3)}/in-service`]: () => json(200, locker(3)),
      }),
    );
    renderMap();

    fireEvent.click(await door("۳"));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("این کمد خارج از سرویس است.");
    fireEvent.click(within(dialog).getByRole("button", { name: "بازگرداندن به سرویس" }));

    await waitFor(() =>
      expect(api.requestsTo("POST", `/api/lockers/${lockerId(3)}/in-service`)).toHaveLength(1),
    );
  });

  // ---- An occupied locker: the visit ----

  function occupiedHandlers(extra: Record<string, Handler> = {}) {
    return mapHandlers(
      allLockers(heldLocker(2, reza.id, reza.fullName)),
      [insideRow(reza.fullName, rezaVisit)],
      extra,
    );
  }

  async function openVisitBox() {
    fireEvent.click(await door("۲"));
    return screen.findByRole("dialog");
  }

  it("OccupiedLocker_Clicked_ShowsTheVisitWithCardioCafeAndEveryAction", async () => {
    mockApi(occupiedHandlers());
    renderMap();

    const dialog = await openVisitBox();

    expect(within(dialog).getByRole("heading", { name: "کمد شماره ۲" })).toBeInTheDocument();
    expect(within(dialog).getByRole("link", { name: "رضا احمدی" })).toHaveAttribute(
      "href",
      `/members/${reza.id}`,
    );
    expect(within(dialog).getByText("هوازی")).toBeInTheDocument();
    expect(within(dialog).getByText("بوفه")).toBeInTheDocument();
    expect(await within(dialog).findByText("۳۰ روز · ۱۲ جلسه")).toBeInTheDocument();
    for (const action of ["ثبت خروج", "لغو ورود", "جابه‌جایی کمد"]) {
      expect(within(dialog).getByRole("button", { name: action })).toBeEnabled();
    }
  });

  it("OccupiedLocker_ClickedOutsideTheBox_ClosesIt", async () => {
    mockApi(occupiedHandlers());
    renderMap();

    await openVisitBox();
    await clickOutsideDialog();

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("OccupiedLocker_CheckOut_AsksWithTheKeyToTakeBack", async () => {
    const api = mockApi(
      occupiedHandlers({
        [`POST /api/attendance/${rezaVisit.id}/check-out`]: () => json(200, closedVisit(reza.id)),
      }),
    );
    renderMap();

    fireEvent.click(within(await openVisitBox()).getByRole("button", { name: "ثبت خروج" }));

    const confirm = await screen.findByRole("dialog");
    expect(confirm).toHaveTextContent("آیا از ثبت خروج رضا احمدی مطمئن هستید؟");
    fireEvent.click(within(confirm).getByLabelText("کلید کمد شماره ۲ را تحویل گرفتم"));
    fireEvent.click(within(confirm).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(confirm).findByText("کمد شماره ۲ آزاد شد.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/attendance/${rezaVisit.id}/check-out`)).toHaveLength(1);
  });

  it("OccupiedLocker_Move_PicksAFreeLockerOnTheMapAndSendsIt", async () => {
    const api = mockApi(
      occupiedHandlers({
        [`POST /api/attendance/${rezaVisit.id}/move-locker`]: () =>
          json(200, { ...rezaVisit, lockerId: lockerId(40), lockerNumber: 40 }),
      }),
    );
    renderMap();

    const dialog = await openVisitBox();
    fireEvent.click(within(dialog).getByRole("button", { name: "جابه‌جایی کمد" }));

    // Only free lockers can be picked: the member's own is held, so it is not one of them.
    expect(within(dialog).getByRole("button", { name: /^کمد ۲،/ })).toBeDisabled();
    fireEvent.click(within(dialog).getByRole("button", { name: /^کمد ۴۰،/ }));
    expect(dialog).toHaveTextContent("رضا احمدی از کمد ۲ به کمد ۴۰ منتقل شود؟");
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، منتقل شود" }));

    expect(await within(dialog).findByText("کمد جابه‌جا شد")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("کمد شماره ۴۰")).toBeInTheDocument();
    const moves = api.requestsTo("POST", `/api/attendance/${rezaVisit.id}/move-locker`);
    expect(moves).toHaveLength(1);
    expect(await moves[0]!.clone().json()).toEqual({ lockerId: lockerId(40) });
  });

  // ---- Reserve places ----

  const reserveVisit = {
    ...openVisit(reza.id),
    lockerId: null,
    lockerNumber: null,
    usesReservePlace: true,
  };

  it("ReservePlaces_Collapsed_ShowOnlyHowManyAreUsed", async () => {
    mockApi(mapHandlers(allLockers(), [insideRow(reza.fullName, reserveVisit)]));
    renderMap();

    const control = await screen.findByRole("button", { name: /ورود بدون کمد ۱ از ۱۵/ });

    expect(control).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("list", { name: "جاهای ورود بدون کمد" })).not.toBeInTheDocument();
  });

  it("ReservePlaces_WhileALockerIsFree_EmptyOnesAreLockedAndSayWhy", async () => {
    mockApi(mapHandlers(allLockers(), [insideRow(reza.fullName, reserveVisit)]));
    renderMap();

    fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));

    const places = screen.getByRole("list", { name: "جاهای ورود بدون کمد" });
    expect(within(places).getByRole("button", { name: "رضا احمدی" })).toBeEnabled();
    const empty = within(places).getAllByRole("button", { name: "جای خالی ورود بدون کمد" });
    expect(empty).toHaveLength(14);
    expect(empty[0]).toBeDisabled();
    expect(screen.getByText(/تا وقتی کمد آزادی هست، ورود بدون کمد ممکن نیست/)).toBeInTheDocument();
  });

  it("ReservePlaces_EveryLockerFull_ChecksInWithNoLocker", async () => {
    const full = Array.from({ length: 72 }, (_, index) =>
      locker(index + 1, { isOutOfService: true }),
    );
    const api = mockApi(
      mapHandlers(full, [], {
        "GET /api/members": () => membersPage([reza]),
        [`POST /api/members/${reza.id}/attendance/check-in`]: () => json(201, { ...reserveVisit }),
      }),
    );
    renderMap();

    fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));
    fireEvent.click(screen.getAllByRole("button", { name: "جای خالی ورود بدون کمد" })[0]!);
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("heading", { name: "ورود بدون کمد" })).toBeInTheDocument();
    // A reserve place has no number, so "who had it today" means nothing (BUSINESS_RULES.md §6).
    expect(
      within(dialog).queryByRole("button", { name: "تاریخچه امروز این کمد" }),
    ).not.toBeInTheDocument();
    await chooseAndConfirm(dialog, "رضا احمدی");

    expect(await within(dialog).findByText(/ورود بدون کمد ثبت شد/)).toBeInTheDocument();
    const requests = api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`);
    expect(await requests[0]!.clone().json()).toEqual({ lockerId: null });
  });

  it("ReservePlaces_UsedPlaceClicked_OpensTheSameVisitBoxAsALocker", async () => {
    mockApi(mapHandlers(allLockers(), [insideRow(reza.fullName, reserveVisit)]));
    renderMap();

    fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));
    fireEvent.click(screen.getByRole("button", { name: "رضا احمدی" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("heading", { name: "ورود بدون کمد" })).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "جابه‌جایی کمد" })).toBeInTheDocument();
  });
});
