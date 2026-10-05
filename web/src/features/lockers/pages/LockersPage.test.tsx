import { act, fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Member } from "@/features/members/api";
import { chooseBirthDate } from "@/test/jalaliDate";
import { cafePage } from "@/test/cafe";
import {
  closedVisit,
  currentlyInsidePage,
  insideRow,
  openVisit,
  todayByHour,
} from "@/test/attendance";
import { clickOutsideDialog } from "@/test/dialog";
import {
  allLockers,
  heldLocker,
  locker,
  lockerId,
  lockersPage,
  lockerUsage,
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

/**
 * When the visits below checked in: as the tests start, so none of them is a long stay (BUSINESS_RULES.md
 * §6 *Long stay*) unless a test says so.
 */
const justNow = new Date().toISOString();

/** Reza's open visit on locker 2, as the "currently inside" list carries it. */
const rezaVisit = {
  ...openVisit(reza.id),
  lockerId: lockerId(2),
  lockerNumber: 2,
  checkedInAt: justNow,
};

function mapHandlers(
  lockers = allLockers(),
  inside: ReturnType<typeof insideRow>[] = [],
  extra: Record<string, Handler> = {},
): Record<string, Handler> {
  return {
    ...signedInHandlers(staffUser),
    "GET /api/lockers": () => lockersPage(lockers),
    "GET /api/attendance/currently-inside": () => currentlyInsidePage(inside),
    "GET /api/attendance/today-by-hour": () => todayByHour(),
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

    // The counts above the map, and how full the lockers in service are (§6 *The desk screen's look*).
    const stats = screen.getByRole("region", { name: "وضعیت کمدها" });
    const legend = within(stats).getByRole("list", { name: "راهنمای کمدها" });
    expect(legend).toHaveTextContent("۷۰ آزاد");
    expect(legend).toHaveTextContent("۱ اشغال");
    expect(legend).toHaveTextContent("۱ خارج از سرویس");
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

    // A holder who owes nothing, and a free door, carry no label. (The legend above the map
    // shows the tag once more, to say what it means.)
    expect(await door("۲")).not.toHaveTextContent("بدهکار");
    const doors = screen.getAllByRole("button", { name: /^کمد / });
    expect(doors.filter((element) => element.textContent?.includes("بدهکار"))).toHaveLength(1);
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

  // ---- The look: alive (BUSINESS_RULES.md §6 *The desk screen's look*) ----

  it("Look_ScreenOpen_DoesNotTurnTheAppDark", async () => {
    mockApi(mapHandlers());
    renderMap();

    await door("۱");
    // The map no longer picks a theme of its own; only the header's switch does (§14).
    expect(document.documentElement).not.toHaveClass("dark");
  });

  it("Look_RefreshChangesADoor_PulsesThatDoorOnly", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      let refreshes = 0;
      mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/lockers": () =>
            lockersPage(
              refreshes++ === 0 ? allLockers() : allLockers(heldLocker(5, reza.id, reza.fullName)),
            ),
        }),
      );
      renderMap();

      // The first drawing changes nothing: every door is new to the eye then.
      expect(await door("۵")).not.toHaveAttribute("data-changed");

      await act(() => vi.advanceTimersByTimeAsync(15_000));

      await waitFor(async () => expect(await door("۵")).toHaveAttribute("data-state", "occupied"));
      expect(await door("۵")).toHaveAttribute("data-changed", "true");
      expect(await door("۶")).not.toHaveAttribute("data-changed");
    } finally {
      vi.useRealTimers();
    }
  });

  // ---- Finding a member on the map (BUSINESS_RULES.md §6) ----

  describe("NameSearch", () => {
    function searchMap(text: string) {
      fireEvent.change(screen.getByRole("searchbox", { name: "جستجوی نام در نقشه" }), {
        target: { value: text },
      });
    }

    function twoHolders() {
      return mapHandlers(
        allLockers(heldLocker(2, reza.id, reza.fullName), heldLocker(67, ali.id, ali.fullName)),
      );
    }

    it("NameSearch_PartOfAName_LiftsThatDoorAndFadesEveryOther", async () => {
      mockApi(twoHolders());
      renderMap();
      await door("۲");

      searchMap("احمد");

      expect(await door("۲")).toHaveAttribute("data-found", "true");
      expect(await door("۶۷")).toHaveAttribute("data-found", "false");
      expect(await door("۱")).toHaveAttribute("data-found", "false");
      expect(screen.getByText("۱ نفر پیدا شد")).toBeInTheDocument();
      // On the legend's row, not in the page's header.
      expect(
        within(screen.getByRole("region", { name: "وضعیت کمدها" })).getByRole("searchbox"),
      ).toBeInTheDocument();
      // A way to find someone, not a filter: a faded door still opens its box.
      fireEvent.click(await door("۱"));
      expect(await screen.findByRole("dialog")).toBeInTheDocument();
    });

    it("NameSearch_ArabicLetters_FindThePersianName", async () => {
      mockApi(twoHolders());
      renderMap();
      await door("۲");

      // «علي» with an Arabic ye finds «علی رضایی» (§13).
      searchMap("علي");

      expect(await door("۶۷")).toHaveAttribute("data-found", "true");
      expect(await door("۲")).toHaveAttribute("data-found", "false");
    });

    it("NameSearch_ALockerNumber_FindsNobody", async () => {
      mockApi(twoHolders());
      renderMap();
      await door("۲");

      searchMap("۶۷");

      expect(await door("۶۷")).toHaveAttribute("data-found", "false");
      expect(screen.getByText("کسی با این نام داخل نیست")).toBeInTheDocument();
    });

    it("NameSearch_OneCharacterOrEscape_LeavesTheMapAsItIs", async () => {
      mockApi(twoHolders());
      renderMap();
      await door("۲");

      searchMap("ر");
      expect(await door("۲")).not.toHaveAttribute("data-found");

      searchMap("رضا");
      expect(await door("۲")).toHaveAttribute("data-found", "true");
      fireEvent.keyDown(screen.getByRole("searchbox", { name: "جستجوی نام در نقشه" }), {
        key: "Escape",
      });

      expect(screen.getByRole("searchbox", { name: "جستجوی نام در نقشه" })).toHaveValue("");
      expect(await door("۲")).not.toHaveAttribute("data-found");
    });

    it("NameSearch_MemberOnAReservePlace_OpensThePlacesAndMarksTheirs", async () => {
      mockApi(
        mapHandlers(allLockers(), [
          insideRow(reza.fullName, {
            ...openVisit(reza.id),
            lockerId: null,
            lockerNumber: null,
            usesReservePlace: true,
            checkedInAt: justNow,
          }),
        ]),
      );
      renderMap();
      await door("۱");
      expect(screen.queryByRole("list", { name: "جاهای ورود بدون کمد" })).not.toBeInTheDocument();

      searchMap("رضا احمدی");

      const places = screen.getByRole("list", { name: "جاهای ورود بدون کمد" });
      expect(within(places).getByRole("button", { name: "رضا احمدی" })).toHaveAttribute(
        "data-found",
        "true",
      );
    });
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
    expect(await within(dialog).findByText("۱۲ جلسه - ۳۰ روزه")).toBeInTheDocument();
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
      sale: { kind: "Membership", sessionCount: 12 },
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
    // By its text: the member's debt ("این عضو بدهی ندارد.") is a status too, once it has loaded.
    const notice = within(dialog).getByText(
      "اشتراک فریز بود و با این ورود از حالت فریز خارج شد؛ ۴ روز به پایان آن اضافه شد.",
    );
    expect(notice.closest("[role='status']")).not.toBeNull();
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
    chooseBirthDate("1370/05/12", dialog);
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
    expect(await within(dialog).findByText("۱۲ جلسه - ۳۰ روزه")).toBeInTheDocument();
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

  it("OccupiedLocker_TodayHistory_ListsWhoHadItThenGoesBackToTheVisit", async () => {
    const api = mockApi(
      occupiedHandlers({
        [`GET /api/lockers/${lockerId(2)}/today`]: () =>
          json(200, [lockerVisit({ memberId: ali.id, memberFullName: ali.fullName })]),
      }),
    );
    renderMap();

    const dialog = await openVisitBox();
    // Nothing is fetched until the desk asks.
    expect(api.requestsTo("GET", `/api/lockers/${lockerId(2)}/today`)).toHaveLength(0);
    fireEvent.click(within(dialog).getByRole("button", { name: "تاریخچه امروز این کمد" }));

    const list = await within(dialog).findByRole("list", { name: "تاریخچه امروز کمد" });
    expect(within(list).getByRole("link", { name: ali.fullName })).toHaveAttribute(
      "href",
      `/members/${ali.id}`,
    );

    fireEvent.click(within(dialog).getByRole("button", { name: "بازگشت" }));
    expect(await within(dialog).findByRole("button", { name: "ثبت خروج" })).toBeEnabled();
  });

  describe("OccupiedLocker plan", () => {
    // 08:00 UTC on 25 September is the same day in Tehran: five days before the plan's last day.
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.setSystemTime(new Date("2026-09-25T08:00:00Z"));
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("OccupiedLocker_Clicked_ShowsTheSessionsBesideTheNameAndThePlanPeriod", async () => {
      mockApi(occupiedHandlers());
      renderMap();

      const dialog = await openVisitBox();

      const sessions = within(dialog).getByRole("region", { name: "جلسات" });
      expect(sessions).toHaveTextContent("۴ از ۱۲");
      expect(sessions).toHaveTextContent("۸ جلسه مانده");
      // activeSubscription runs from 1 September to 30 September 2026, both days included.
      expect(await within(dialog).findByText("۱۴۰۵/۰۶/۱۰ تا ۱۴۰۵/۰۷/۰۸")).toBeInTheDocument();
      expect(within(dialog).getByText("دوره اعتبار")).toBeInTheDocument();
      // Five days is the desk panel's renewal threshold, so it is marked the same way.
      expect(within(dialog).getByText("۵ روز مانده")).toHaveClass("text-destructive");
      // The header already shows the sessions; the plan box does not repeat them.
      expect(within(dialog).queryByText("جلسات باقی‌مانده")).not.toBeInTheDocument();
    });
  });

  // ---- Reserve places ----

  const reserveVisit = {
    ...openVisit(reza.id),
    lockerId: null,
    lockerNumber: null,
    usesReservePlace: true,
    checkedInAt: justNow,
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

  it("ReservePlaces_UsedOneOpened_OffersNoLockerHistory", async () => {
    mockApi(mapHandlers(allLockers(), [insideRow(reza.fullName, reserveVisit)]));
    renderMap();

    fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));
    fireEvent.click(
      within(screen.getByRole("list", { name: "جاهای ورود بدون کمد" })).getByRole("button", {
        name: "رضا احمدی",
      }),
    );

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByRole("button", { name: "ثبت خروج" })).toBeEnabled();
    // A reserve place is not a locker: there is no one else's day on it to show.
    expect(
      within(dialog).queryByRole("button", { name: "تاریخچه امروز این کمد" }),
    ).not.toBeInTheDocument();
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

  // ---- The desk panel: birthdays and renewal opportunities (BUSINESS_RULES.md §6) ----

  describe("DeskPanel", () => {
    // ۱۴۰۵/۰۵/۱۲, late morning in Tehran: a fixed "today" for birthdays and days left.
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.setSystemTime(new Date("2026-08-03T08:00:00Z"));
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    /** Ali's open visit on locker 67, comfortably far from running out unless a test says otherwise. */
    const aliVisit = {
      ...openVisit(ali.id),
      id: "0199a000-0000-7000-8000-0000000000c8",
      lockerId: lockerId(67),
      lockerNumber: 67,
    };

    function panelHandlers(
      rezaRow: Parameters<typeof insideRow>[2],
      aliRow: Parameters<typeof insideRow>[2] = {},
    ) {
      return mapHandlers(
        allLockers(heldLocker(2, reza.id, reza.fullName), heldLocker(67, ali.id, ali.fullName)),
        [insideRow(reza.fullName, rezaVisit, rezaRow), insideRow(ali.fullName, aliVisit, aliRow)],
      );
    }

    async function panelSection(name: string) {
      const panel = await screen.findByRole("complementary", { name: "پنل پذیرش" });
      return within(panel).getByRole("region", { name });
    }

    it("DeskPanel_SessionsRunningOut_ListsThemUnderRenewalWithWhatIsLeft", async () => {
      mockApi(panelHandlers({ usedSessions: 10, remainingSessions: 2 }));
      renderMap();

      const renewals = await panelSection("فرصت تمدید");
      expect(within(renewals).getByRole("heading")).toHaveTextContent("فرصت تمدید (۱)");
      const entry = within(renewals).getByRole("button", { name: /رضا احمدی/ });
      expect(entry).toHaveTextContent("کمد ۲");
      expect(entry).toHaveTextContent("۲ جلسه مانده");
      expect(within(renewals).queryByText(ali.fullName)).not.toBeInTheDocument();
    });

    it("DeskPanel_EndingWithinFiveDays_SaysHowManyDaysAreLeft", async () => {
      mockApi(panelHandlers({ subscriptionEndDate: "2026-08-06" }));
      renderMap();

      const renewals = await panelSection("فرصت تمدید");
      expect(within(renewals).getByRole("button", { name: /رضا احمدی/ })).toHaveTextContent(
        "۳ روز مانده",
      );
    });

    it("DeskPanel_AlreadyRenewedOrASingleVisit_IsNotListedAndThePanelIsHidden", async () => {
      mockApi(
        panelHandlers(
          { usedSessions: 11, remainingSessions: 1, hasQueuedRenewal: true },
          { isSingleSession: true, totalSessions: 1, usedSessions: 1, remainingSessions: 0 },
        ),
      );
      renderMap();

      expect(await door("۲")).toHaveAttribute("data-state", "occupied");
      expect(screen.queryByRole("complementary", { name: "پنل پذیرش" })).not.toBeInTheDocument();
    });

    it("DeskPanel_JalaliBirthdayToday_ListsThemUnderHappyBirthday", async () => {
      // Born ۱۳۷۰/۰۵/۱۲; ali's birthday is another day.
      mockApi(panelHandlers({ memberBirthDate: "1991-08-03" }, { memberBirthDate: "1991-08-04" }));
      renderMap();

      const birthdays = await panelSection("تولدت مبارک");
      expect(within(birthdays).getByRole("button", { name: /رضا احمدی/ })).toHaveTextContent(
        "کمد ۲",
      );
      expect(within(birthdays).queryByText(ali.fullName)).not.toBeInTheDocument();
      // Nobody is running out, so that list is not shown at all.
      expect(screen.queryByRole("region", { name: "فرصت تمدید" })).not.toBeInTheDocument();
      // Their door celebrates, and says why to a screen reader; ali's does not.
      const party = await door("۲");
      expect(party).toHaveAttribute("data-birthday", "true");
      expect(within(party).getByTestId("party-ring")).toBeInTheDocument();
      expect(party).toHaveAccessibleName("کمد ۲، اشغال — رضا احمدی، امروز تولدش است");
      const other = await door("۶۷");
      expect(other).not.toHaveAttribute("data-birthday");
      expect(within(other).queryByTestId("party-ring")).not.toBeInTheDocument();
    });

    it("DeskPanel_BirthdayOnAReservePlace_ThePlaceCelebratesToo", async () => {
      mockApi(
        mapHandlers(allLockers(), [
          insideRow(
            reza.fullName,
            {
              ...openVisit(reza.id),
              lockerId: null,
              lockerNumber: null,
              usesReservePlace: true,
              checkedInAt: new Date().toISOString(),
            },
            { memberBirthDate: "1991-08-03" },
          ),
        ]),
      );
      renderMap();

      fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));
      const place = screen.getByRole("button", { name: "رضا احمدی، امروز تولدش است" });
      expect(place).toHaveAttribute("data-birthday", "true");
    });

    it("DeskPanel_EntryPointedAt_MakesItsLockerBlinkUntilLeft", async () => {
      mockApi(panelHandlers({ usedSessions: 10, remainingSessions: 2 }));
      renderMap();

      const entry = within(await panelSection("فرصت تمدید")).getByRole("button", {
        name: /رضا احمدی/,
      });
      fireEvent.mouseEnter(entry);

      expect(await door("۲")).toHaveAttribute("data-highlighted", "true");
      expect(await door("۶۷")).not.toHaveAttribute("data-highlighted");

      fireEvent.mouseLeave(entry);

      expect(await door("۲")).not.toHaveAttribute("data-highlighted");
    });

    it("DeskPanel_EntryClicked_OpensThatLockersBox", async () => {
      mockApi(panelHandlers({ usedSessions: 10, remainingSessions: 2 }));
      renderMap();

      fireEvent.click(
        within(await panelSection("فرصت تمدید")).getByRole("button", { name: /رضا احمدی/ }),
      );

      const dialog = await screen.findByRole("dialog");
      expect(within(dialog).getByRole("heading", { name: "کمد شماره ۲" })).toBeInTheDocument();
    });
  });

  // ---- Long stay: the bar along the bottom of a door (BUSINESS_RULES.md §6) ----

  describe("LongStay", () => {
    const now = new Date("2026-09-29T09:00:00Z");
    const checkedInBefore = (minutes: number) =>
      new Date(now.getTime() - minutes * 60_000).toISOString();

    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.setSystemTime(now);
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    function onLocker2(checkedInAt: string) {
      return mapHandlers(allLockers(heldLocker(2, reza.id, reza.fullName)), [
        insideRow(reza.fullName, { ...rezaVisit, checkedInAt }),
      ]);
    }

    function stayBar(element: HTMLElement) {
      return within(element).getByTestId("stay-bar");
    }

    it("LongStay_OneAndAHalfHoursIn_BarIsHalfFullWithNoWarning", async () => {
      mockApi(onLocker2(checkedInBefore(90)));
      renderMap();

      const held = await door("۲");
      const bar = stayBar(held);
      const fill = within(bar).getByTestId("stay-bar-fill");
      expect(fill).toHaveStyle({ width: "50%" });
      // Halfway from green to red, and still: only a long stay blinks.
      expect(fill.style.getPropertyValue("--stay-mix")).toBe("50%");
      expect(fill).not.toHaveClass("animate-stay-blink");
      expect(bar).not.toHaveAttribute("data-long");
      expect(held).toHaveAccessibleName("کمد ۲، اشغال — رضا احمدی");
      // Only a held door carries a bar.
      expect(screen.getAllByTestId("stay-bar")).toHaveLength(1);
    });

    it("LongStay_AMinutePassesAtThreeHours_BarTurnsToTheWarningAndTheLabelSaysIt", async () => {
      mockApi(onLocker2(checkedInBefore(179)));
      renderMap();

      expect(stayBar(await door("۲"))).not.toHaveAttribute("data-long");

      // No new request: the page's own clock moves the bar on.
      await act(() => vi.advanceTimersByTimeAsync(60_000));

      const held = await door("۲");
      const bar = stayBar(held);
      expect(bar).toHaveAttribute("data-long", "true");
      const fill = within(bar).getByTestId("stay-bar-fill");
      expect(fill).toHaveStyle({ width: "100%" });
      // Wholly red now, blinking faintly (still for anyone who asked for less motion).
      expect(fill.style.getPropertyValue("--stay-mix")).toBe("100%");
      expect(fill).toHaveClass("animate-stay-blink", "motion-reduce:animate-none");
      expect(held).toHaveAccessibleName("کمد ۲، اشغال — رضا احمدی، بیش از ۳ ساعت");
      // Nothing is written on the door for it.
      expect(held).not.toHaveTextContent("بیش از ۳ ساعت");
    });

    it("LongStay_ReservePlaceThreeHoursIn_ShowsTheSameWarning", async () => {
      mockApi(
        mapHandlers(allLockers(), [
          insideRow(reza.fullName, {
            ...openVisit(reza.id),
            lockerId: null,
            lockerNumber: null,
            usesReservePlace: true,
            checkedInAt: checkedInBefore(200),
          }),
        ]),
      );
      renderMap();

      fireEvent.click(await screen.findByRole("button", { name: /ورود بدون کمد/ }));
      const place = screen.getByRole("button", { name: "رضا احمدی، بیش از ۳ ساعت" });
      expect(stayBar(place)).toHaveAttribute("data-long", "true");
    });
  });

  // ---- Today by hour: the chart under the map (BUSINESS_RULES.md §6) ----

  describe("TodayByHour", () => {
    // 14:40 UTC is 18:10 in Tehran: hour 18 is the one it is now.
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      vi.setSystemTime(new Date("2026-09-30T14:40:00Z"));
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    async function chart() {
      return screen.findByRole("region", { name: "ورود امروز ساعت به ساعت" });
    }

    function hourRows(region: HTMLElement) {
      return within(within(region).getByRole("table")).getAllByRole("row").slice(1);
    }

    it("TodayByHour_Counts_DrawsTheBusyHoursWithTodayAndTheAverage", async () => {
      mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/attendance/today-by-hour": () =>
            todayByHour({ 7: { average: 1.5 }, 9: { today: 2, average: 3 }, 18: { today: 4 } }),
        }),
      );
      renderMap();

      const region = await chart();
      // From the first busy hour to the last, the quiet ones between kept; the night left out.
      const rows = hourRows(region);
      expect(rows).toHaveLength(12);
      expect(rows[0]).toHaveTextContent("ساعت ۷ تا ۸");
      expect(rows[2]).toHaveTextContent("ساعت ۹ تا ۱۰۲۳");
      expect(rows[11]).toHaveTextContent("ساعت ۱۸ تا ۱۹۴۰");
      expect(within(region).getByText("میانگین چهارشنبه‌های ۴ هفتهٔ گذشته")).toBeInTheDocument();

      const bars = within(region).getByTestId("today-by-hour-bars");
      const nine = bars.querySelector('[data-hour="9"]');
      expect(nine?.querySelector("title")).toHaveTextContent("ساعت ۹ تا ۱۰: امروز ۲، میانگین ۳");
      const seven = bars.querySelector('[data-hour="7"]');
      expect(seven?.querySelector("title")).toHaveTextContent("امروز ۰، میانگین ۱٫۵");
      // The hour it is now on the gym's clock is marked.
      expect(bars.querySelector("[data-now]")).toHaveAttribute("data-hour", "18");
      // Time runs from the right: the first hour is drawn further right than the last.
      const xOf = (hour: number) =>
        Number(bars.querySelector(`[data-hour="${hour}"] rect`)?.getAttribute("x"));
      expect(xOf(7)).toBeGreaterThan(xOf(18));
    });

    it("TodayByHour_SomePastDaysClosed_SaysHowManyOpenDaysTheAverageCovers", async () => {
      mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/attendance/today-by-hour": () =>
            todayByHour({ 9: { today: 1, average: 2 } }, { daysAveraged: 3 }),
        }),
      );
      renderMap();

      expect(
        within(await chart()).getByText("میانگین ۳ چهارشنبه باز در ۴ هفتهٔ گذشته"),
      ).toBeInTheDocument();
    });

    it("TodayByHour_NoPastDays_ShowsTodayWithoutAnAverage", async () => {
      mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/attendance/today-by-hour": () =>
            todayByHour({ 9: { today: 1 } }, { daysAveraged: 0 }),
        }),
      );
      renderMap();

      const region = await chart();
      expect(within(region).getByText("هنوز میانگینی از هفته‌های گذشته نیست")).toBeInTheDocument();
      expect(
        within(region).queryByRole("columnheader", { name: "میانگین" }),
      ).not.toBeInTheDocument();
    });

    it("TodayByHour_NothingYet_SaysSo", async () => {
      mockApi(mapHandlers());
      renderMap();

      const region = await chart();
      expect(within(region).getByText("امروز هنوز ورودی ثبت نشده است.")).toBeInTheDocument();
      expect(within(region).queryByTestId("today-by-hour-bars")).not.toBeInTheDocument();
    });

    it("TodayByHour_RequestFails_OneQuietLineAndTheMapStillWorks", async () => {
      mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/attendance/today-by-hour": () => problem(500, "General.Unexpected"),
        }),
      );
      renderMap();

      // The app retries a failed request once, a second later, before giving up.
      const line = await screen.findByText(
        "نمودار ورود امروز بارگذاری نشد.",
        {},
        { timeout: 3000 },
      );
      expect(await chart()).toContainElement(line);
      expect(await door("۱")).toHaveAttribute("data-state", "free");
    });

    it("TodayByHour_CheckIn_FetchesTheCountsAgain", async () => {
      const api = mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/members": () => membersPage([reza]),
          [`POST /api/members/${reza.id}/attendance/check-in`]: () =>
            json(201, { ...openVisit(reza.id), lockerId: lockerId(1), lockerNumber: 1 }),
        }),
      );
      renderMap();
      await chart();
      const before = api.requestsTo("GET", "/api/attendance/today-by-hour").length;

      await chooseAndConfirm(await openFreeLocker("۱"), "رضا احمدی");

      await waitFor(() =>
        expect(api.requestsTo("GET", "/api/attendance/today-by-hour").length).toBeGreaterThan(
          before,
        ),
      );
    });
  });

  describe("UsageMap", () => {
    function usageHandlers(uses: Partial<Record<number, number>> = {}) {
      return mapHandlers(
        allLockers(heldLocker(2, reza.id, reza.fullName), locker(3, { isOutOfService: true })),
        [insideRow(reza.fullName, rezaVisit)],
        { "GET /api/lockers/usage": () => lockerUsage(uses) },
      );
    }

    async function switchOn() {
      fireEvent.click(await screen.findByRole("button", { name: "نقشهٔ استفاده" }));
    }

    /** A door in the usage view: a picture, not a button. */
    async function usageDoor(number: string) {
      return screen.findByRole("img", { name: new RegExp(`^کمد ${number}،`) });
    }

    it("UsageMap_SwitchedOn_ShadesEachDoorAgainstTheMostUsedWithItsCount", async () => {
      const api = mockApi(usageHandlers({ 5: 40, 6: 20, 7: 1 }));
      renderMap();

      await switchOn();

      const most = await usageDoor("۵");
      expect(most).toHaveAccessibleName("کمد ۵، ۴۰ بار استفاده");
      expect(most).toHaveAttribute("data-usage-level", "5");
      expect(most).toHaveTextContent("۴۰ بار");
      expect(await usageDoor("۶")).toHaveAttribute("data-usage-level", "3");
      // Used once beside a locker used 40 times: still shaded, never taken for unused.
      expect(await usageDoor("۷")).toHaveAttribute("data-usage-level", "1");
      const unused = await usageDoor("۸");
      expect(unused).toHaveAccessibleName("کمد ۸، استفاده نشده");
      expect(unused).toHaveAttribute("data-usage-level", "0");
      // An out-of-service locker says so, which explains its zero.
      expect(await usageDoor("۳")).toHaveAccessibleName("کمد ۳، استفاده نشده، خارج از سرویس");

      expect(screen.getByRole("button", { name: "نقشهٔ استفاده" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(screen.getByRole("button", { name: "۳۰ روز اخیر" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(screen.getByRole("list", { name: "راهنمای نقشهٔ استفاده" })).toHaveTextContent(
        "بیشترین ۴۰ بار",
      );
      const [request] = api.requestsTo("GET", "/api/lockers/usage");
      expect(new URL(request!.url).searchParams.get("Days")).toBe("30");
    });

    it("UsageMap_On_ShowsNothingOfWhoIsInsideAndNothingCanBeClicked", async () => {
      mockApi(usageHandlers({ 2: 3 }));
      renderMap();
      await door("۲");

      await switchOn();

      const held = await usageDoor("۲");
      expect(held).toHaveTextContent("۳ بار");
      expect(screen.queryByText(reza.fullName)).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: /^کمد / })).not.toBeInTheDocument();
      expect(screen.getAllByRole("img", { name: /^کمد / })).toHaveLength(72);
      expect(
        screen.queryByRole("searchbox", { name: "جستجوی نام در نقشه" }),
      ).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: /ورود بدون کمد/ })).not.toBeInTheDocument();
      expect(screen.queryByRole("list", { name: "راهنمای کمدها" })).not.toBeInTheDocument();

      fireEvent.click(held);
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });

    it("UsageMap_PeriodPicked_FetchesThoseDaysAndMarksThePick", async () => {
      const api = mockApi(usageHandlers({ 5: 2 }));
      renderMap();
      await switchOn();
      await usageDoor("۵");

      fireEvent.click(screen.getByRole("button", { name: "۷ روز اخیر" }));

      await waitFor(() =>
        expect(
          api
            .requestsTo("GET", "/api/lockers/usage")
            .map((request) => new URL(request.url).searchParams.get("Days")),
        ).toEqual(["30", "7"]),
      );
      expect(screen.getByRole("button", { name: "۷ روز اخیر" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(screen.getByRole("button", { name: "۳۰ روز اخیر" })).toHaveAttribute(
        "aria-pressed",
        "false",
      );
    });

    it("UsageMap_SwitchedOff_BringsTheDesksMapBack", async () => {
      const api = mockApi(usageHandlers());
      renderMap();
      await switchOn();
      await usageDoor("۵");

      fireEvent.click(screen.getByRole("button", { name: "نقشهٔ استفاده" }));

      expect(await door("۲")).toHaveAttribute("data-state", "occupied");
      expect(screen.getByRole("searchbox", { name: "جستجوی نام در نقشه" })).toBeInTheDocument();
      expect(screen.getByRole("list", { name: "راهنمای کمدها" })).toBeInTheDocument();
      expect(screen.queryByRole("img", { name: /^کمد / })).not.toBeInTheDocument();
      // Never asked for until the view is turned on, and not polled once it is off.
      expect(api.requestsTo("GET", "/api/lockers/usage")).toHaveLength(1);
    });

    it("UsageMap_Off_NeverAsksForTheCounts", async () => {
      const api = mockApi(usageHandlers());
      renderMap();

      await door("۱");

      expect(api.requestsTo("GET", "/api/lockers/usage")).toHaveLength(0);
    });

    it("UsageMap_RequestFails_SaysSoInPlaceOfTheMap", async () => {
      mockApi({
        ...usageHandlers(),
        "GET /api/lockers/usage": () => problem(500, "General.Unexpected"),
      });
      renderMap();

      await switchOn();

      expect(await screen.findByRole("alert", {}, { timeout: 3000 })).toBeInTheDocument();
      expect(screen.queryByRole("img", { name: /^کمد / })).not.toBeInTheDocument();
    });
  });
});
