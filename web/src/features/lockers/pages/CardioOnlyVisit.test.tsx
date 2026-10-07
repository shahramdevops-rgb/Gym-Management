import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import {
  cardioCharge,
  closedVisit,
  currentlyInsidePage,
  insideRow,
  openVisit,
  todayByHour,
} from "@/test/attendance";
import { cafePage } from "@/test/cafe";
import { allLockers, cardioLocker, lockerId, lockersPage } from "@/test/lockers";
import {
  json,
  mockApi,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { memberDebt, membersPage, reza } from "@/test/members";
import { pricesResponse } from "@/test/prices";
import { renderApp } from "@/test/renderApp";
import { activeSubscription, subscriptionsPage } from "@/test/subscriptions";

// «ورود فقط هوازی» on the desk's map (BUSINESS_RULES.md §7 *Cardio-only visit*), as Staff: letting
// a member in without a session, with or without a plan, their door in yellow, and their box, where
// check-out waits for the هوازی amount.

const justNow = new Date().toISOString();

const cardioOnlyPath = `/api/members/${reza.id}/attendance/cardio-only-check-in`;

const checkInPath = `/api/members/${reza.id}/attendance/check-in`;

/** Reza on locker 4, in only for هوازی, with no amount recorded yet. */
const rezaCardioVisit = {
  ...openVisit(reza.id),
  lockerId: lockerId(4),
  lockerNumber: 4,
  isCardioOnly: true,
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

/** A locker's door on the map, by its number. The comma keeps "کمد ۴،" from matching "کمد ۴۰،". */
async function door(number: string) {
  return screen.findByRole("button", { name: new RegExp(`^کمد ${number}،`) });
}

/** Opens a free locker's box and chooses Reza, which brings up the confirmation. */
async function chooseReza(number: string) {
  fireEvent.click(await door(number));
  const dialog = await screen.findByRole("dialog");
  fireEvent.change(within(dialog).getByRole("searchbox", { name: "نام یا شماره موبایل" }), {
    target: { value: "رضا" },
  });
  fireEvent.click(await within(dialog).findByRole("button", { name: /رضا احمدی/ }));
  return dialog;
}

describe("Cardio-only visit", () => {
  // ---- Letting the member in ----

  it("CheckIn_CardioOnlyConfirmed_SendsTheLockerToItsOwnEndpointAndSaysSo", async () => {
    const api = mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        [`POST ${cardioOnlyPath}`]: () => json(201, rezaCardioVisit),
      }),
    );
    renderMap();

    const dialog = await chooseReza("۴");
    fireEvent.click(await within(dialog).findByRole("button", { name: "ورود فقط هوازی" }));

    await waitFor(() =>
      expect(dialog).toHaveTextContent(
        "آیا از ثبت ورود فقط هوازی رضا احمدی با کمد شماره ۴ مطمئن هستید؟ جلسه‌ای کم نمی‌شود و اشتراک لازم نیست.",
      ),
    );
    expect(api.requestsTo("POST", cardioOnlyPath)).toHaveLength(0);
    fireEvent.click(within(dialog).getByRole("button", { name: "بله، ورود فقط هوازی ثبت شود" }));

    expect(await within(dialog).findByText("ورود فقط هوازی ثبت شد")).toBeInTheDocument();
    expect(within(dialog).getByLabelText("کمد شماره ۴")).toBeInTheDocument();
    const requests = api.requestsTo("POST", cardioOnlyPath);
    expect(requests).toHaveLength(1);
    expect(await requests[0]!.clone().json()).toEqual({ lockerId: lockerId(4) });
    expect(api.requestsTo("POST", `/api/members/${reza.id}/attendance/check-in`)).toHaveLength(0);
  });

  it("CheckIn_CardioOnlyBack_ReturnsToTheOrdinaryConfirmation", async () => {
    mockApi(mapHandlers(allLockers(), [], { "GET /api/members": () => membersPage([reza]) }));
    renderMap();

    const dialog = await chooseReza("۴");
    fireEvent.click(await within(dialog).findByRole("button", { name: "ورود فقط هوازی" }));
    fireEvent.click(await within(dialog).findByRole("button", { name: "انصراف" }));

    expect(
      await within(dialog).findByRole("button", { name: "بله، ورود ثبت شود" }),
    ).toBeInTheDocument();
  });

  it("CheckIn_CardioOnlyRefused_SaysWhyAndOffersNoSale", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        [`POST ${cardioOnlyPath}`]: () => problem(422, "Members.Inactive"),
      }),
    );
    renderMap();

    const dialog = await chooseReza("۴");
    fireEvent.click(await within(dialog).findByRole("button", { name: "ورود فقط هوازی" }));
    fireEvent.click(
      await within(dialog).findByRole("button", { name: "بله، ورود فقط هوازی ثبت شود" }),
    );

    expect(await within(dialog).findByRole("alert")).toHaveTextContent(
      "رضا احمدی: این عضو غیرفعال است.",
    );
    expect(
      within(dialog).queryByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).not.toBeInTheDocument();
  });

  // ---- No plan needed (roadmap 6.5.35) ----

  it.each(["Attendance.NoSubscription", "Subscriptions.Expired", "Subscriptions.NoSessionsLeft"])(
    "CheckIn_RefusedWith%s_OffersCardioOnlyUnderTheSaleAndLetsThemIn",
    async (refusal) => {
      const api = mockApi(
        mapHandlers(allLockers(), [], {
          "GET /api/members": () => membersPage([reza]),
          "GET /api/pricing": () => pricesResponse(),
          [`POST ${checkInPath}`]: () => problem(422, refusal),
          [`POST ${cardioOnlyPath}`]: () => json(201, { ...rezaCardioVisit, subscriptionId: null }),
        }),
      );
      renderMap();

      const dialog = await chooseReza("۴");
      fireEvent.click(await within(dialog).findByRole("button", { name: "بله، ورود ثبت شود" }));
      // The sale is still offered, and cardio-only beside it.
      expect(
        await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
      ).toBeInTheDocument();
      fireEvent.click(within(dialog).getByRole("button", { name: "ورود فقط هوازی" }));
      fireEvent.click(
        await within(dialog).findByRole("button", { name: "بله، ورود فقط هوازی ثبت شود" }),
      );

      expect(await within(dialog).findByText("ورود فقط هوازی ثبت شد")).toBeInTheDocument();
      expect(api.requestsTo("POST", cardioOnlyPath)).toHaveLength(1);
      // Only the refused ordinary check-in: nothing was sold.
      expect(api.requestsTo("POST", checkInPath)).toHaveLength(1);
    },
  );

  it("CheckIn_CardioOnlyFromTheSaleBack_ReturnsToTheSale", async () => {
    mockApi(
      mapHandlers(allLockers(), [], {
        "GET /api/members": () => membersPage([reza]),
        "GET /api/pricing": () => pricesResponse(),
        [`POST ${checkInPath}`]: () => problem(422, "Attendance.NoSubscription"),
      }),
    );
    renderMap();

    const dialog = await chooseReza("۴");
    fireEvent.click(await within(dialog).findByRole("button", { name: "بله، ورود ثبت شود" }));
    await within(dialog).findByText("ورود ممکن نیست");
    fireEvent.click(within(dialog).getByRole("button", { name: "ورود فقط هوازی" }));
    fireEvent.click(await within(dialog).findByRole("button", { name: "انصراف" }));

    expect(
      await within(dialog).findByRole("button", { name: /ورود تک‌جلسه‌ای/ }),
    ).toBeInTheDocument();
    expect(within(dialog).getByText("ورود ممکن نیست")).toBeInTheDocument();
  });

  // ---- On the map ----

  it("Map_CardioOnlyHolder_DrawsTheDoorAsCardioWithItsLegend", async () => {
    mockApi(
      mapHandlers(allLockers(cardioLocker(4, reza.id, reza.fullName)), [
        insideRow(reza.fullName, rezaCardioVisit),
      ]),
    );
    renderMap();

    const held = await door("۴");
    expect(held).toHaveAccessibleName("کمد ۴، هوازی — رضا احمدی");
    expect(held).toHaveAttribute("data-state", "cardio");
    expect(screen.getByRole("list", { name: "راهنمای کمدها" })).toHaveTextContent("هوازی");
  });

  // ---- The visit's box ----

  it("Visit_CardioOnlyWithoutAnAmount_MarksItAndKeepsCheckOutDisabled", async () => {
    mockApi(
      mapHandlers(allLockers(cardioLocker(4, reza.id, reza.fullName)), [
        insideRow(reza.fullName, rezaCardioVisit),
      ]),
    );
    renderMap();

    fireEvent.click(await door("۴"));
    const dialog = await screen.findByRole("dialog");

    expect(within(dialog).getByText("فقط هوازی")).toBeInTheDocument();
    expect(within(dialog).getByRole("status")).toHaveTextContent(
      "خروج پس از ثبت مبلغ هوازی ممکن است",
    );
    expect(within(dialog).getByRole("button", { name: "ثبت خروج" })).toBeDisabled();
    expect(within(dialog).getByRole("button", { name: "لغو ورود" })).toBeEnabled();
  });

  it("Visit_CardioOnlyWithAnAmount_ChecksOut", async () => {
    const charged = { ...rezaCardioVisit, serviceCharges: [cardioCharge(rezaCardioVisit)] };
    const api = mockApi(
      mapHandlers(
        allLockers(cardioLocker(4, reza.id, reza.fullName)),
        [insideRow(reza.fullName, charged)],
        {
          [`POST /api/attendance/${charged.id}/check-out`]: () => json(200, closedVisit(reza.id)),
        },
      ),
    );
    renderMap();

    fireEvent.click(await door("۴"));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).queryByRole("status")).not.toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت خروج" }));

    const confirm = await screen.findByRole("dialog");
    fireEvent.click(within(confirm).getByLabelText("کلید کمد شماره ۴ را تحویل گرفتم"));
    fireEvent.click(within(confirm).getByRole("button", { name: "بله، خروج ثبت شود" }));

    expect(await within(confirm).findByText("کمد شماره ۴ آزاد شد.")).toBeInTheDocument();
    expect(api.requestsTo("POST", `/api/attendance/${charged.id}/check-out`)).toHaveLength(1);
  });

  it("Visit_CardioOnlyWithNoPlan_SaysSoInsteadOfABar", async () => {
    mockApi(
      mapHandlers(allLockers(cardioLocker(4, reza.id, reza.fullName)), [
        insideRow(reza.fullName, rezaCardioVisit, {
          subscriptionId: null,
          totalSessions: null,
          usedSessions: null,
          remainingSessions: null,
          subscriptionEndDate: null,
        }),
      ]),
    );
    renderMap();

    fireEvent.click(await door("۴"));
    const dialog = await screen.findByRole("dialog");

    const sessions = within(dialog).getByRole("region", { name: "جلسات" });
    expect(sessions).toHaveTextContent("بدون اشتراک");
    expect(sessions).not.toHaveTextContent("جلسه مانده");
  });

  it("Visit_OrdinaryWithoutAnAmount_ChecksOutAsBefore", async () => {
    const ordinary = { ...rezaCardioVisit, isCardioOnly: false };
    mockApi(
      mapHandlers(allLockers(cardioLocker(4, reza.id, reza.fullName)), [
        insideRow(reza.fullName, ordinary),
      ]),
    );
    renderMap();

    fireEvent.click(await door("۴"));
    const dialog = await screen.findByRole("dialog");

    expect(within(dialog).queryByText("فقط هوازی")).not.toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "ثبت خروج" })).toBeEnabled();
  });
});
