import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Member } from "@/features/members/api";
import { cafePage } from "@/test/cafe";
import { closedVisit, currentlyInsidePage, insideRow, openVisit } from "@/test/attendance";
import { allLockers, heldLocker, locker, lockerId, lockersPage } from "@/test/lockers";
import {
  json,
  mockApi,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { debtItem, memberDebt, membersPage, reza, serviceChargeDebtItem } from "@/test/members";
import { plansPage, singleSession } from "@/test/plans";
import { renderApp } from "@/test/renderApp";
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

    expect(await door("۱")).toHaveAttribute("data-state", "free");
    const held = await door("۲");
    expect(held).toHaveAttribute("data-state", "occupied");
    // The holder's name, on hover and for a screen reader.
    expect(held).toHaveAttribute("title", "رضا احمدی");
    expect(held).toHaveAccessibleName("کمد ۲، اشغال — رضا احمدی");
    expect(await door("۳")).toHaveAttribute("data-state", "outOfService");

    const legend = screen.getByRole("list", { name: "راهنمای کمدها" });
    expect(legend).toHaveTextContent("آزاد: ۷۰");
    expect(legend).toHaveTextContent("اشغال: ۱");
    expect(legend).toHaveTextContent("خارج از سرویس: ۱");
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
    expect(await within(dialog).findByText("یک ماهه ۱۲ جلسه")).toBeInTheDocument();
    expect(await within(dialog).findByRole("region", { name: "بدهی" })).toHaveTextContent("هوازی");

    const requests = api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`);
    expect(requests).toHaveLength(1);
    expect(await requests[0]!.clone().json()).toEqual({ lockerId: lockerId(12) });
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

  it("CheckIn_NoSubscription_SellsASingleVisitAndChecksInWithTheSameLocker", async () => {
    let sold = false;
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        "GET /api/plans": () => plansPage([singleSession]),
        [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
          sold
            ? json(201, { ...openVisit(reza.id), lockerId: lockerId(12), lockerNumber: 12 })
            : problem(422, "Attendance.NoSubscription"),
        [`POST /api/members/${reza.id}/subscriptions`]: () => {
          sold = true;
          return json(201, { ...activeSubscription, isSingleSession: true });
        },
      }),
    );
    renderMap();

    const dialog = await openFreeLocker("۱۲");
    await chooseAndConfirm(dialog, "رضا احمدی");
    fireEvent.click(await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }));

    expect(await within(dialog).findByText("ورود تک‌جلسه‌ای ثبت شد")).toBeInTheDocument();
    const checkIns = api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`);
    expect(checkIns).toHaveLength(2);
    expect(await checkIns[1]!.clone().json()).toEqual({ lockerId: lockerId(12) });
  });

  it("CheckIn_NobodyFound_RegistersThemAndCarriesOnToTheCheckIn", async () => {
    const created = { ...reza, fullName: "سارا محمدی" };
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([]),
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

    await waitFor(() =>
      expect(dialog).toHaveTextContent("آیا از ثبت ورود سارا محمدی با کمد شماره ۱۲ مطمئن هستید؟"),
    );
    expect(api.requestsTo("POST", "/api/members")).toHaveLength(1);
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
    expect(await within(dialog).findByText("یک ماهه ۱۲ جلسه")).toBeInTheDocument();
    for (const action of ["ثبت خروج", "لغو ورود", "جابه‌جایی کمد"]) {
      expect(within(dialog).getByRole("button", { name: action })).toBeEnabled();
    }
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
