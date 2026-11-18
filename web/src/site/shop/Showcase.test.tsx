import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";

import { json, mockApi, problem } from "@/test/mockApi";

import type { Showcase, ShowcaseProduct } from "./api";
import { shopRoutes } from "./routes";

const supplements = "0199b000-0000-7000-8000-000000000001";
const wear = "0199b000-0000-7000-8000-000000000002";
const creatine = "0199b000-0000-7000-8000-000000000011";
const shirt = "0199b000-0000-7000-8000-000000000012";
const photo = "0199b000-0000-7000-8000-000000000021";
const secondPhoto = "0199b000-0000-7000-8000-000000000022";

const noContacts = {
  mobilePhone: null,
  landlinePhone: null,
  whatsAppPhone: null,
  telegram: null,
  rubika: null,
  eitaa: null,
  bale: null,
  instagram: null,
};

const showcase: Showcase = {
  categories: [
    { id: wear, name: "پوشاک" },
    { id: supplements, name: "مکمل" },
  ],
  products: [
    {
      id: shirt,
      name: "تی‌شرت ورزشی",
      categoryId: wear,
      mainPhotoId: null,
      minPrice: 400000,
      maxPrice: 450000,
      inStock: false,
    },
    {
      id: creatine,
      name: "کراتین",
      categoryId: supplements,
      mainPhotoId: photo,
      minPrice: 900000,
      maxPrice: 900000,
      inStock: true,
    },
  ],
  contacts: { ...noContacts, mobilePhone: "+989121234567", telegram: "pasargad_shop" },
};

const creatinePage: ShowcaseProduct = {
  id: creatine,
  name: "کراتین",
  description: "هر پیمانه ۵ گرم.\nروزی یک پیمانه.",
  categoryId: supplements,
  categoryName: "مکمل",
  variants: [
    { id: "0199b000-0000-7000-8000-000000000031", label: "۳۰۰ گرم", price: 900000, inStock: true },
    {
      id: "0199b000-0000-7000-8000-000000000032",
      label: "۵۰۰ گرم",
      price: 1300000,
      inStock: false,
    },
  ],
  photos: [
    { id: photo, width: 1600, height: 1200 },
    { id: secondPhoto, width: 1200, height: 1600 },
  ],
  contacts: { ...noContacts, mobilePhone: "+989121234567" },
};

/** The shop's routes at an address under its base, with a cache of the test's own. */
function renderShop(url = "/") {
  const router = createMemoryRouter(shopRoutes, { initialEntries: [url] });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  return router;
}

describe("the shop's showcase", () => {
  it("Showcase_WithoutSigningIn_ShowsEachProductWithItsPrice", async () => {
    const api = mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    renderShop();

    const cards = within(await screen.findByRole("list", { name: "کالاهای فروشگاه" }));
    const creatineCard = cards.getByRole("link", { name: /کراتین/ });
    expect(creatineCard).toHaveAttribute("href", `/product/${creatine}`);
    expect(creatineCard).toHaveTextContent("۹۰۰٬۰۰۰ تومان");
    expect(creatineCard.querySelector("img")).toHaveAttribute(
      "src",
      `/api/site/shop/photos/${photo}/small`,
    );
    // No token of any kind goes with the request: the showcase is public.
    expect(api.requestsTo("GET", "/api/site/shop/showcase")[0]!.headers.has("Authorization")).toBe(
      false,
    );
    expect(api.unexpected).toEqual([]);
  });

  it("Showcase_AProductOutOfStockWithTwoPrices_SaysSoAndShowsTheLowest", async () => {
    mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    renderShop();

    const card = await screen.findByRole("link", { name: /تی‌شرت ورزشی/ });
    expect(card).toHaveTextContent("ناموجود");
    expect(card).toHaveTextContent("از ۴۰۰٬۰۰۰ تومان");
    expect(screen.getByRole("link", { name: /کراتین/ })).not.toHaveTextContent("ناموجود");
  });

  it("Showcase_ACategoryChosen_ShowsOnlyItsProducts", async () => {
    mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    renderShop(`/?category=${supplements}`);

    expect(await screen.findByRole("link", { name: /کراتین/ })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /تی‌شرت ورزشی/ })).not.toBeInTheDocument();
    const chips = within(screen.getByRole("navigation", { name: "دسته‌بندی" }));
    expect(chips.getByRole("link", { name: "مکمل" })).toHaveAttribute("aria-current", "page");
    expect(chips.getByRole("link", { name: "همه" })).not.toHaveAttribute("aria-current");

    fireEvent.click(chips.getByRole("link", { name: "همه" }));

    expect(await screen.findByRole("link", { name: /تی‌شرت ورزشی/ })).toBeInTheDocument();
  });

  it("Showcase_ACategoryThatIsGone_ShowsEverything", async () => {
    mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    renderShop("/?category=0199b000-0000-7000-8000-00000000dead");

    expect(await screen.findByRole("link", { name: /کراتین/ })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /تی‌شرت ورزشی/ })).toBeInTheDocument();
  });

  it("Showcase_TheContactWays_AreLinksAndEmptyOnesAreLeftOut", async () => {
    mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    renderShop();

    const ways = within(
      await screen.findByRole("region", { name: "راه‌های تماس با فروشگاه" }),
    ).getAllByRole("link");
    expect(ways).toHaveLength(2);
    expect(ways[0]).toHaveAttribute("href", "tel:+989121234567");
    expect(ways[0]).toHaveTextContent("۰۹۱۲ ۱۲۳ ۴۵۶۷");
    expect(ways[1]).toHaveAttribute("href", "https://t.me/pasargad_shop");
    expect(ways[1]).toHaveAttribute("target", "_blank");
  });

  it("Shop_TheFoot_HasTheEnamadDetailsAndThePages", async () => {
    mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    renderShop();

    const foot = within(await screen.findByRole("contentinfo"));
    // Tests run as development, where the public page is served under /public-site/.
    const pages = within(foot.getByRole("navigation", { name: "صفحه‌های سایت" })).getAllByRole(
      "link",
    );
    expect(pages.map((link) => link.textContent)).toEqual([
      "فروشگاه",
      "دربارهٔ ما",
      "تماس با ما",
      "قوانین",
      "بازگشت کالا",
      "حریم خصوصی",
    ]);
    expect(pages[2]).toHaveAttribute("href", "/public-site/contact/");
    expect(pages[4]).toHaveAttribute("href", "/public-site/returns/");
    expect(foot.getByText(/کدپستی ۶۷۱۵۷۶۵۸۶۱/)).toBeInTheDocument();
    expect(foot.getByRole("link", { name: "۰۸۳ ۳۴۲۴ ۵۰۳۱" })).toHaveAttribute(
      "href",
      "tel:+988334245031",
    );
    expect(foot.getByRole("link", { name: "javad.persiangulf@gmail.com" })).toHaveAttribute(
      "href",
      "mailto:javad.persiangulf@gmail.com",
    );
  });

  it("Shop_BeforeBuyingOpens_SaysSoOnTheShowcaseAndAProductsPage", async () => {
    mockApi({
      "GET /api/site/shop/showcase": () => json(200, showcase),
      [`GET /api/site/shop/products/${creatine}`]: () => json(200, creatinePage),
    });
    const soon = /خرید آنلاین به‌زودی باز می‌شود/;

    const router = renderShop();
    expect(await screen.findByRole("note")).toHaveTextContent(soon);

    await router.navigate(`/product/${creatine}`);
    await screen.findByRole("heading", { level: 1, name: "کراتین" });
    expect(screen.getByRole("note")).toHaveTextContent(soon);
    // No way to buy on the page yet.
    expect(screen.queryByRole("button", { name: /خرید|سبد/ })).not.toBeInTheDocument();
  });

  it("Showcase_NothingToShow_SaysSoWithoutChips", async () => {
    mockApi({
      "GET /api/site/shop/showcase": () =>
        json(200, { categories: [], products: [], contacts: noContacts }),
    });

    renderShop();

    expect(await screen.findByText("هنوز کالایی در فروشگاه نیست.")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "دسته‌بندی" })).not.toBeInTheDocument();
    expect(
      screen.queryByRole("region", { name: "راه‌های تماس با فروشگاه" }),
    ).not.toBeInTheDocument();
  });

  it("Showcase_TheServerDoesNotAnswer_ShowsTheMessage", async () => {
    mockApi({
      "GET /api/site/shop/showcase": () => {
        throw new TypeError("Failed to fetch");
      },
    });

    renderShop();

    expect(await screen.findByRole("alert")).toHaveTextContent("ارتباط با سرور برقرار نشد");
  });

  it("Product_AShownOne_ShowsItsVariantsPhotosAndDescription", async () => {
    mockApi({ [`GET /api/site/shop/products/${creatine}`]: () => json(200, creatinePage) });

    renderShop(`/product/${creatine}`);

    expect(await screen.findByRole("heading", { level: 1, name: "کراتین" })).toBeInTheDocument();
    const variants = within(screen.getByRole("list", { name: "مدل‌ها و قیمت" })).getAllByRole(
      "listitem",
    );
    expect(variants[0]).toHaveTextContent("۳۰۰ گرم");
    expect(variants[0]).toHaveTextContent("۹۰۰٬۰۰۰ تومان");
    expect(variants[0]).toHaveTextContent("موجود");
    expect(variants[1]).toHaveTextContent("۱٬۳۰۰٬۰۰۰ تومان");
    expect(variants[1]).toHaveTextContent("ناموجود");
    expect(screen.getByText(/هر پیمانه ۵ گرم/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "مکمل" })).toHaveAttribute(
      "href",
      `/?category=${supplements}`,
    );
    expect(document.title).toBe("کراتین | فروشگاه پاسارگاد پلاس");

    // The first photo is the one shown; a small one under it chooses another.
    expect(screen.getByRole("img", { name: "کراتین" })).toHaveAttribute(
      "src",
      `/api/site/shop/photos/${photo}/large`,
    );
    fireEvent.click(screen.getByRole("button", { name: "عکس ۲" }));
    expect(screen.getByRole("img", { name: "کراتین" })).toHaveAttribute(
      "src",
      `/api/site/shop/photos/${secondPhoto}/large`,
    );
  });

  it("Product_WithOneUnnamedVariant_ShowsThePriceAlone", async () => {
    const single: ShowcaseProduct = {
      ...creatinePage,
      photos: [],
      variants: [{ id: creatinePage.variants[0]!.id, label: "", price: 900000, inStock: true }],
    };
    mockApi({ [`GET /api/site/shop/products/${creatine}`]: () => json(200, single) });

    renderShop(`/product/${creatine}`);

    expect(await screen.findByText("۹۰۰٬۰۰۰ تومان")).toBeInTheDocument();
    expect(screen.getByText("موجود")).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "مدل‌ها و قیمت" })).not.toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "عکس‌های کالا" })).not.toBeInTheDocument();
  });

  it("Product_HiddenOrUnknown_SaysNotFoundAndLeadsBack", async () => {
    mockApi({
      [`GET /api/site/shop/products/${creatine}`]: () => problem(404, "ShopProducts.NotFound"),
    });

    renderShop(`/product/${creatine}`);

    expect(await screen.findByText("این کالا پیدا نشد.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "همهٔ کالاها" })).toHaveAttribute("href", "/");
  });

  it("Product_AnAddressCutShort_SaysNotFoundWithoutAskingTheApi", async () => {
    const api = mockApi({});

    renderShop("/product/0199b000-0000-7000-8000-00000000001");

    expect(await screen.findByText("این کالا پیدا نشد.")).toBeInTheDocument();
    expect(screen.queryByText("در حال بارگذاری…")).not.toBeInTheDocument();
    expect(api.fetchMock).not.toHaveBeenCalled();
  });

  it("Shop_AnAddressThatIsNoPage_GoesToTheShowcase", async () => {
    mockApi({ "GET /api/site/shop/showcase": () => json(200, showcase) });

    const router = renderShop("/sign-in");

    expect(await screen.findByRole("link", { name: /کراتین/ })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/");
  });
});
