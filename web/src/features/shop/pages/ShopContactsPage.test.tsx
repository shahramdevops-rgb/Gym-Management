import { fireEvent, screen, within } from "@testing-library/react";

import type { ShopContacts } from "@/features/shop/api";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  shopAdmin,
  signedInHandlers,
  staffUser,
  tokenWithRoles,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const empty: ShopContacts = {
  mobilePhone: null,
  landlinePhone: null,
  whatsAppPhone: null,
  telegram: null,
  rubika: null,
  eitaa: null,
  bale: null,
  instagram: null,
  version: 7,
};

const filled: ShopContacts = {
  ...empty,
  mobilePhone: "+989121234567",
  landlinePhone: "07132345678",
  telegram: "pasargad_gym",
};

const shopSession = () => session({ accessToken: tokenWithRoles("Shop") });

describe("ShopContactsPage", () => {
  it("ShopContacts_ShopAdmin_SavesTheWaysCleaned", async () => {
    const api = mockApi({
      ...signedInHandlers(shopAdmin),
      "GET /api/shop/contacts": () => json(200, empty),
      "PUT /api/shop/contacts": () => json(200, { ...filled, version: 8 }),
    });
    renderApp("/shop/contacts", { session: shopSession() });

    fireEvent.change(await screen.findByLabelText("تلفن همراه"), {
      target: { value: "۰۹۱۲۱۲۳۴۵۶۷" },
    });
    fireEvent.change(screen.getByLabelText("تلفن ثابت"), { target: { value: "۰۷۱-۳۲۳۴-۵۶۷۸" } });
    fireEvent.change(screen.getByLabelText("تلگرام"), { target: { value: "@pasargad_gym" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText("تغییرها ثبت شد.")).toBeInTheDocument();
    const body = (await api.requestsTo("PUT", "/api/shop/contacts")[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body).toEqual({
      mobilePhone: "09121234567",
      landlinePhone: "07132345678",
      whatsAppPhone: null,
      telegram: "pasargad_gym",
      rubika: null,
      eitaa: null,
      bale: null,
      instagram: null,
      version: 7,
    });
  });

  it("ShopContacts_IdWithALink_IsRejectedBeforeSending", async () => {
    const api = mockApi({
      ...signedInHandlers(shopAdmin),
      "GET /api/shop/contacts": () => json(200, empty),
    });
    renderApp("/shop/contacts", { session: shopSession() });

    fireEvent.change(await screen.findByLabelText("اینستاگرام"), {
      target: { value: "https://instagram.com/gym" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    expect(await screen.findByText(/@ و لینک لازم نیست/)).toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/shop/contacts")).toHaveLength(0);
  });

  it("ShopContacts_MobileRefusedByTheServer_ShowsTheErrorUnderTheMobile", async () => {
    mockApi({
      ...signedInHandlers(shopAdmin),
      "GET /api/shop/contacts": () => json(200, empty),
      "PUT /api/shop/contacts": () => problem(400, "Shop.MobilePhoneInvalid"),
    });
    renderApp("/shop/contacts", { session: shopSession() });

    fireEvent.change(await screen.findByLabelText("تلفن همراه"), {
      target: { value: "+14155550100" },
    });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره" }));

    const message = await screen.findByText(/شمارهٔ همراه باید موبایل ایران باشد/);
    expect(screen.getByLabelText("تلفن همراه")).toHaveAttribute("aria-describedby", message.id);
  });

  it("ShopContacts_Owner_ReadsThemWithoutAForm", async () => {
    const api = mockApi({
      ...signedInHandlers(owner),
      "GET /api/shop/contacts": () => json(200, filled),
    });
    renderApp("/shop/contacts", { session: session() });

    expect(await screen.findByText("این‌ها را فقط مدیر فروشگاه تغییر می‌دهد.")).toBeInTheDocument();
    expect(screen.getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toBeInTheDocument();
    expect(screen.getByText("pasargad_gym")).toBeInTheDocument();
    expect(screen.getAllByText("ثبت نشده").length).toBeGreaterThan(0);
    expect(screen.queryByRole("button", { name: "ذخیره" })).not.toBeInTheDocument();
    expect(api.requestsTo("PUT", "/api/shop/contacts")).toHaveLength(0);
  });

  it("ShopContacts_Staff_SeesNoAccessMessage", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/shop/contacts", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/shop/contacts")).toHaveLength(0);
  });

  it("GymPage_ShopAdmin_IsSentToTheShopWithItsMenuOnly", async () => {
    const api = mockApi({
      ...signedInHandlers(shopAdmin),
      "GET /api/shop/products": () => json(200, []),
      "GET /api/shop/categories": () => json(200, []),
    });

    renderApp("/members", { session: shopSession() });

    // The shop's home since 16.3 is its products.
    expect(await screen.findByRole("heading", { name: "کالاهای فروشگاه" })).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/members")).toHaveLength(0);

    const menu = screen.getAllByRole("navigation")[0]!;
    expect(await within(menu).findByRole("link", { name: "کالاها" })).toBeInTheDocument();
    expect(within(menu).getByRole("link", { name: "دسته‌بندی‌ها" })).toBeInTheDocument();
    expect(within(menu).getByRole("link", { name: "راه‌های تماس" })).toBeInTheDocument();
    expect(within(menu).getByRole("link", { name: "تغییر رمز عبور" })).toBeInTheDocument();
    expect(within(menu).queryByRole("link", { name: "اعضا" })).not.toBeInTheDocument();
    expect(within(menu).queryByRole("link", { name: "ورود با کمد" })).not.toBeInTheDocument();
  });

  it("Menu_ShopAdminBeforeMeAnswers_ShowsNoGymItem", async () => {
    mockApi({
      ...signedInHandlers(shopAdmin),
      // «me» never answers: the token's roles decide the menu and the page.
      "GET /api/auth/me": () => new Promise<Response>(() => {}),
      "GET /api/shop/contacts": () => json(200, empty),
    });

    renderApp("/shop/contacts", { session: shopSession() });

    expect(await screen.findByLabelText("تلفن همراه")).toBeInTheDocument();
    const menu = screen.getAllByRole("navigation")[0]!;
    expect(within(menu).getByRole("link", { name: "راه‌های تماس" })).toBeInTheDocument();
    expect(within(menu).queryByRole("link", { name: "اعضا" })).not.toBeInTheDocument();
    expect(within(menu).queryByRole("link", { name: "ورود با کمد" })).not.toBeInTheDocument();
  });

  it("Menu_Staff_HasNoShop", async () => {
    mockApi({ ...signedInHandlers(staffUser), "GET /api/members": () => json(200, { items: [] }) });

    renderApp("/status", { session: session() });

    const menu = (await screen.findAllByRole("navigation"))[0]!;
    expect(await within(menu).findByRole("link", { name: "اعضا" })).toBeInTheDocument();
    expect(within(menu).queryByRole("link", { name: "راه‌های تماس" })).not.toBeInTheDocument();
  });
});
