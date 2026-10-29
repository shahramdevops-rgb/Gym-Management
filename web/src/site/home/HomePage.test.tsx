import { act, fireEvent, screen, within } from "@testing-library/react";

import { json, mockApi } from "@/test/mockApi";

import { siteSessionStore } from "../auth/session";
import { activeMember, emptyHome, renderSite, siteAccess, trainer } from "../test/renderSite";

describe("HomePage", () => {
  it("Home_ActiveMember_SeesTheirArea", async () => {
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, siteAccess().me) });

    renderSite("/", { access: siteAccess() });

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "اشتراک من" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "شاگردان من" })).not.toBeInTheDocument();
  });

  it("Home_MemberDeactivatedAtTheDesk_SeesOnlyTheNotice", async () => {
    const inactive = { member: { ...activeMember, isActive: false }, trainer: null };
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, inactive) });

    renderSite("/", { access: siteAccess() });

    expect(await screen.findByRole("alert")).toHaveTextContent("عضویت شما در باشگاه غیرفعال است");
    expect(screen.queryByRole("heading", { name: "اشتراک من" })).not.toBeInTheDocument();
  });

  it("Home_TrainerWhoIsAlsoAMember_SeesBothAreas", async () => {
    const both = { member: activeMember, trainer };
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, both) });

    renderSite("/", { access: siteAccess(both) });

    expect(await screen.findByRole("heading", { name: "اشتراک من" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "شاگردان من" })).toBeInTheDocument();
  });

  it("Home_TrainerOnly_SeesTheTrainersAreaUnderTheirName", async () => {
    const trainerOnly = { member: null, trainer };
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, trainerOnly) });

    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByRole("heading", { name: "سلام رضا کریمی" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "شاگردان من" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "اشتراک من" })).not.toBeInTheDocument();
  });

  it("Home_StoppedTrainerWhoIsNotAMember_IsToldAndSignedOut", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, { member: null, trainer: null }),
      "POST /api/site/auth/logout": () => new Response(null, { status: 204 }),
    });

    renderSite("/", { access: siteAccess({ member: null, trainer }) });

    expect(await screen.findByRole("alert")).toHaveTextContent("مربی‌گری شما تمام شده است.");
    expect(screen.getByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/logout")).toHaveLength(1);
    expect(siteSessionStore.getState().status).toBe("signedOut");
  });

  it("Home_NewTokenSaysTrainingEnded_IsToldWithoutAReload", async () => {
    const trainerOnly = { member: null, trainer };
    mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, trainerOnly),
      "POST /api/site/auth/logout": () => new Response(null, { status: 204 }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });
    await screen.findByRole("heading", { name: "شاگردان من" });

    // The refresh 15 minutes later reads the roles again: the Owner stopped this trainer.
    act(() => siteSessionStore.signIn(siteAccess({ member: null, trainer: null })));

    expect(await screen.findByRole("alert")).toHaveTextContent("مربی‌گری شما تمام شده است.");
  });

  it("Home_TrainerWhoseMembershipWasDeactivated_SeesOnlyTheNotice", async () => {
    // §15 (1405/08/06): the API sends no trainer then, so the screen shows the notice alone.
    const inactive = { member: { ...activeMember, isActive: false }, trainer: null };
    mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, inactive) });

    renderSite("/", { access: siteAccess(inactive) });

    expect(await screen.findByRole("alert")).toHaveTextContent("عضویت شما در باشگاه غیرفعال است");
    expect(screen.queryByRole("heading", { name: "شاگردان من" })).not.toBeInTheDocument();
  });

  it("Home_ServerUnreachable_OffersARetryInsteadOfSigningOut", async () => {
    mockApi({ "POST /api/site/auth/refresh": () => json(200, siteAccess()) });
    renderSite("/", { access: siteAccess() });

    act(() => siteSessionStore.unreachable());

    expect(screen.getByRole("alert")).toHaveTextContent("ارتباط با سرور برقرار نشد");
    expect(screen.queryByRole("heading", { name: "ورود یا ثبت‌نام" })).not.toBeInTheDocument();

    mockApi({
      ...emptyHome,
      "POST /api/site/auth/refresh": () => json(200, siteAccess()),
      "GET /api/site/me": () => json(200, siteAccess().me),
    });
    fireEvent.click(screen.getByRole("button", { name: "دوباره امتحان کنید" }));

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
  });

  it("Home_SignOut_EndsTheSessionOnTheServerAndHere", async () => {
    const api = mockApi({
      ...emptyHome,
      "GET /api/site/me": () => json(200, siteAccess().me),
      "POST /api/site/auth/logout": () => new Response(null, { status: 204 }),
    });
    renderSite("/", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "خروج" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("برای ورود دوباره یک کد پیامکی تازه لازم است");
    fireEvent.click(within(dialog).getByRole("button", { name: "خروج" }));

    expect(await screen.findByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/logout")).toHaveLength(1);
  });

  it("Home_SignOutThenCancel_StaysSignedIn", async () => {
    const api = mockApi({ ...emptyHome, "GET /api/site/me": () => json(200, siteAccess().me) });
    renderSite("/", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "خروج" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "انصراف" }),
    );

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/logout")).toHaveLength(0);
  });
});
