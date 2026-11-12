import { fireEvent, screen, within } from "@testing-library/react";

import type { ShopCategory, ShopProduct, ShopProductListItem } from "@/features/shop/api";
import {
  json,
  mockApi,
  owner,
  session,
  shopAdmin,
  signedInHandlers,
  tokenWithRoles,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const shopSession = () => session({ accessToken: tokenWithRoles("Shop") });
const ownerSession = () => session({ accessToken: tokenWithRoles("Owner") });

const supplements: ShopCategory = { id: "c1", name: "مکمل", productCount: 2, version: 3 };
const clothes: ShopCategory = { id: "c2", name: "پوشاک", productCount: 1, version: 4 };

const whey: ShopProductListItem = {
  id: "p1",
  name: "پروتئین وی",
  categoryId: "c1",
  categoryName: "مکمل",
  isVisible: true,
  mainPhotoId: "ph1",
  minPrice: 1200000,
  maxPrice: 2100000,
  totalStock: 4,
  variantCount: 2,
};

const shirt: ShopProductListItem = {
  id: "p2",
  name: "تیشرت باشگاه",
  categoryId: "c2",
  categoryName: "پوشاک",
  isVisible: false,
  mainPhotoId: null,
  minPrice: 450000,
  maxPrice: 450000,
  totalStock: 0,
  variantCount: 1,
};

const wheyProduct: ShopProduct = {
  id: "p1",
  name: "پروتئین وی",
  description: "۲۴ گرم پروتئین",
  categoryId: "c1",
  isVisible: true,
  version: 9,
  variants: [
    { id: "v1", label: "شکلاتی", price: 1200000, stock: 4 },
    { id: "v2", label: "وانیلی", price: 2100000, stock: 0 },
  ],
  photos: [
    { id: "ph1", width: 1600, height: 1200 },
    { id: "ph2", width: 1200, height: 1600 },
  ],
};

const catalogHandlers = {
  "GET /api/shop/categories": () => json(200, [supplements, clothes]),
  "GET /api/shop/products": () => json(200, [whey, shirt]),
};

describe("Shop catalogue", () => {
  it("Products_ShopAdmin_ShowsCardsAndFiltersByCategory", async () => {
    mockApi({ ...signedInHandlers(shopAdmin), ...catalogHandlers });
    renderApp("/shop/products", { session: shopSession() });

    const list = await screen.findByRole("list", { name: "فهرست کالاها" });
    expect(within(list).getByText("پروتئین وی")).toBeInTheDocument();
    expect(within(list).getByText(/۱٬۲۰۰٬۰۰۰.*تا.*۲٬۱۰۰٬۰۰۰/)).toBeInTheDocument();
    expect(within(list).getByText("پنهان")).toBeInTheDocument();
    expect(within(list).getByText("ناموجود")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /کالای تازه/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "پوشاک" }));

    expect(within(list).queryByText("پروتئین وی")).not.toBeInTheDocument();
    expect(within(list).getByText("تیشرت باشگاه")).toBeInTheDocument();
  });

  it("Products_Owner_ReadsWithoutTheNewButton", async () => {
    mockApi({ ...signedInHandlers(owner), ...catalogHandlers });
    renderApp("/shop/products", { session: ownerSession() });

    expect(await screen.findByText("پروتئین وی")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /کالای تازه/ })).not.toBeInTheDocument();
  });

  it("NewProduct_OneVariant_SendsPriceAndStockAsTheApiReadsThem", async () => {
    const created: ShopProduct = {
      ...wheyProduct,
      id: "p9",
      photos: [],
      variants: [{ id: "v9", label: "", price: 1200000, stock: 5 }],
    };
    const api = mockApi({
      ...signedInHandlers(shopAdmin),
      ...catalogHandlers,
      "POST /api/shop/products": () => json(201, created),
      "GET /api/shop/products/p9": () => json(200, created),
    });
    renderApp("/shop/products/new", { session: shopSession() });

    fireEvent.change(await screen.findByLabelText("نام کالا"), { target: { value: " کراتین " } });
    fireEvent.change(screen.getByLabelText("دسته"), { target: { value: "c1" } });
    fireEvent.change(screen.getByLabelText("قیمت (تومان)"), { target: { value: "۱۲۰۰۰۰۰" } });
    fireEvent.change(screen.getByLabelText("موجودی"), { target: { value: "۵" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره و افزودن عکس" }));

    expect(
      await screen.findByText("کالا ذخیره شد. حالا عکس‌هایش را اضافه کنید."),
    ).toBeInTheDocument();
    const body = (await api.requestsTo("POST", "/api/shop/products")[0]!.json()) as Record<
      string,
      unknown
    >;
    expect(body).toEqual({
      name: "کراتین",
      description: null,
      categoryId: "c1",
      isVisible: true,
      variants: [{ id: null, label: "", price: "1200000", stock: 5 }],
    });
  });

  it("NewProduct_TwoVariantsOneUnnamed_IsRefusedUnderThatVariant", async () => {
    const api = mockApi({ ...signedInHandlers(shopAdmin), ...catalogHandlers });
    renderApp("/shop/products/new", { session: shopSession() });

    fireEvent.change(await screen.findByLabelText("نام کالا"), { target: { value: "پروتئین" } });
    fireEvent.change(screen.getByLabelText("دسته"), { target: { value: "c1" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن مدل" }));
    const prices = screen.getAllByLabelText("قیمت (تومان)");
    const stocks = screen.getAllByLabelText("موجودی");
    prices.forEach((price) => fireEvent.change(price, { target: { value: "1000000" } }));
    stocks.forEach((stock) => fireEvent.change(stock, { target: { value: "1" } }));
    fireEvent.change(screen.getByLabelText("نام مدل ۱"), { target: { value: "شکلاتی" } });
    fireEvent.click(screen.getByRole("button", { name: "ذخیره و افزودن عکس" }));

    expect(
      await screen.findByText("وقتی کالا بیش از یک مدل دارد، هر مدل نام لازم دارد."),
    ).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/shop/products")).toHaveLength(0);
  });

  it("Product_ShopAdmin_UploadsAPhotoAndShowsIt", async () => {
    // jsdom's FormData is not the one Node's Request accepts, so the request would fail before it
    // left; a stand-in that Request does accept lets the upload's flow run. The real multipart is
    // covered by the API's tests (ShopCatalogTests) and the walk in the browser.
    vi.stubGlobal(
      "FormData",
      class extends URLSearchParams {
        override append(name: string, value: string | Blob) {
          super.append(name, value instanceof File ? value.name : String(value));
        }
      },
    );
    const withThird: ShopProduct = {
      ...wheyProduct,
      photos: [...wheyProduct.photos, { id: "ph3", width: 800, height: 800 }],
    };
    const api = mockApi({
      ...signedInHandlers(shopAdmin),
      ...catalogHandlers,
      "GET /api/shop/products/p1": () => json(200, wheyProduct),
      "POST /api/shop/products/p1/photos": () => json(200, withThird),
    });
    renderApp("/shop/products/p1", { session: shopSession() });

    expect(await screen.findByAltText("عکس ۱ از پروتئین وی")).toBeInTheDocument();
    const file = new File(["fake"], "whey.jpg", { type: "image/jpeg" });
    fireEvent.change(screen.getByLabelText("افزودن عکس"), { target: { files: [file] } });

    expect(await screen.findByAltText("عکس ۳ از پروتئین وی")).toBeInTheDocument();
    const request = api.requestsTo("POST", "/api/shop/products/p1/photos")[0]!;
    expect(new URLSearchParams(await request.text()).get("file")).toBe("whey.jpg");
  });

  it("Product_Owner_ReadsItWithoutAForm", async () => {
    mockApi({
      ...signedInHandlers(owner),
      ...catalogHandlers,
      "GET /api/shop/products/p1": () => json(200, wheyProduct),
    });
    renderApp("/shop/products/p1", { session: ownerSession() });

    expect(await screen.findByText("وانیلی")).toBeInTheDocument();
    expect(screen.getByText("این‌ها را فقط مدیر فروشگاه تغییر می‌دهد.")).toBeInTheDocument();
    expect(screen.queryByLabelText("نام کالا")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("افزودن عکس")).not.toBeInTheDocument();
  });

  it("Categories_ShopAdmin_AddsOneAndCannotDeleteOneWithProducts", async () => {
    const api = mockApi({
      ...signedInHandlers(shopAdmin),
      "GET /api/shop/categories": () => json(200, [supplements]),
      "POST /api/shop/categories": () =>
        json(201, { id: "c3", name: "لوازم", productCount: 0, version: 1 }),
    });
    renderApp("/shop/categories", { session: shopSession() });

    expect(await screen.findByRole("button", { name: "حذف مکمل" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("دستهٔ تازه"), { target: { value: "لوازم" } });
    fireEvent.click(screen.getByRole("button", { name: "افزودن" }));

    expect(await screen.findByText("دستهٔ «لوازم» اضافه شد.")).toBeInTheDocument();
    expect(await api.requestsTo("POST", "/api/shop/categories")[0]!.json()).toEqual({
      name: "لوازم",
    });
  });
});
