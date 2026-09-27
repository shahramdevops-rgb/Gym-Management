import { fireEvent, screen, waitFor } from "@testing-library/react";

import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { prices, pricesNotSet, pricesResponse } from "@/test/prices";
import { renderApp } from "@/test/renderApp";

describe("SettingsPage", () => {
  it("SettingsPage_StaffUser_SeesNoAccessMessage", async () => {
    // BUSINESS_RULES.md §1: Staff sell at the prices but only the Owner changes them.
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/settings", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/pricing")).toHaveLength(0);
  });

  it("SettingsPage_StaffUser_HasNoMenuItem", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/lockers": () => json(200, { items: [] }) });

    renderApp("/", { session: session() });

    await screen.findByRole("link", { name: "ورود با کمد" });
    expect(screen.queryByRole("link", { name: "تنظیمات" })).not.toBeInTheDocument();
  });

  it("SettingsPage_PricesNotSetYet_SaysTheDeskCannotSellAndShowsEmptyBoxes", async () => {
    mockApi({ ...signedInHandlers(owner), "GET /api/pricing": () => pricesResponse(pricesNotSet) });

    renderApp("/settings", { session: session() });

    expect(
      await screen.findByText(/پذیرش نمی‌تواند پلن یا ورود تک‌جلسه‌ای بفروشد/),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("قیمت هر جلسه برای پلن‌ها (تومان)")).toHaveValue("");
    expect(screen.getByLabelText("قیمت تک‌جلسهٔ آزاد (تومان)")).toHaveValue("");
  });

  it("SettingsPage_Save_SendsBothPricesWithTheVersionAndSaysSaved", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/pricing": () => pricesResponse(pricesNotSet),
      "PUT /api/pricing": () => json(200, { ...prices, version: 2 }),
    });
    renderApp("/settings", { session: session() });

    fireEvent.change(await screen.findByLabelText("قیمت هر جلسه برای پلن‌ها (تومان)"), {
      target: { value: "۷۵٬۰۰۰" },
    });
    fireEvent.change(screen.getByLabelText("قیمت تک‌جلسهٔ آزاد (تومان)"), {
      target: { value: "150000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("قیمت‌ها ذخیره شد.")).toBeInTheDocument();
    const saves = api.requestsTo("PUT", "/api/pricing");
    expect(saves).toHaveLength(1);
    expect(await saves[0]!.clone().json()).toEqual({
      sessionPrice: "75000",
      singleVisitPrice: "150000",
      version: pricesNotSet.version,
    });
  });

  it("SettingsPage_EmptyPrice_IsRefusedBeforeAnythingIsSent", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/pricing": () => pricesResponse(pricesNotSet),
    });
    renderApp("/settings", { session: session() });

    fireEvent.change(await screen.findByLabelText("قیمت هر جلسه برای پلن‌ها (تومان)"), {
      target: { value: "75000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("قیمت را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/pricing")).toHaveLength(0);
  });

  it("SettingsPage_SomeoneSavedMeanwhile_SaysSoAndOffersToReload", async () => {
    mockApi({
      ...signedInHandlers(owner),
      "GET /api/pricing": () => pricesResponse(prices),
      "PUT /api/pricing": () => problem(409, "Pricing.ChangedConcurrently"),
    });
    renderApp("/settings", { session: session() });

    await waitFor(() =>
      expect(screen.getByLabelText("قیمت هر جلسه برای پلن‌ها (تومان)")).not.toHaveValue(""),
    );
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText(/قیمت‌ها هم‌زمان توسط شخص دیگری تغییر کرد/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "بارگذاری اطلاعات تازه" })).toBeInTheDocument();
  });
});
