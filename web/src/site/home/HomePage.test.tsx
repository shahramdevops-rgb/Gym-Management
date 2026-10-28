import { act, fireEvent, screen } from "@testing-library/react";

import { json, mockApi } from "@/test/mockApi";

import { siteSessionStore } from "../auth/session";
import { activeMember, renderSite, siteAccess, trainer } from "../test/renderSite";

describe("HomePage", () => {
  it("Home_ActiveMember_SeesTheirArea", async () => {
    mockApi({ "GET /api/site/me": () => json(200, siteAccess().me) });

    renderSite("/", { access: siteAccess() });

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "عضویت من" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "بخش مربی" })).not.toBeInTheDocument();
  });

  it("Home_MemberDeactivatedAtTheDesk_SeesOnlyTheNotice", async () => {
    const inactive = { member: { ...activeMember, isActive: false }, trainer: null };
    mockApi({ "GET /api/site/me": () => json(200, inactive) });

    renderSite("/", { access: siteAccess() });

    expect(await screen.findByRole("alert")).toHaveTextContent("عضویت شما در باشگاه غیرفعال است");
    expect(screen.queryByRole("heading", { name: "عضویت من" })).not.toBeInTheDocument();
  });

  it("Home_TrainerWhoIsAlsoAMember_SeesBothAreas", async () => {
    const both = { member: activeMember, trainer };
    mockApi({ "GET /api/site/me": () => json(200, both) });

    renderSite("/", { access: siteAccess(both) });

    expect(await screen.findByRole("heading", { name: "عضویت من" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "بخش مربی" })).toBeInTheDocument();
  });

  it("Home_TrainerOnly_SeesTheTrainersAreaUnderTheirName", async () => {
    const trainerOnly = { member: null, trainer };
    mockApi({ "GET /api/site/me": () => json(200, trainerOnly) });

    renderSite("/", { access: siteAccess(trainerOnly) });

    expect(await screen.findByRole("heading", { name: "سلام رضا کریمی" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "بخش مربی" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "عضویت من" })).not.toBeInTheDocument();
  });

  it("Home_StoppedTrainerWhoIsNotAMember_IsToldAndSignedOut", async () => {
    const api = mockApi({
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
      "GET /api/site/me": () => json(200, trainerOnly),
      "POST /api/site/auth/logout": () => new Response(null, { status: 204 }),
    });
    renderSite("/", { access: siteAccess(trainerOnly) });
    await screen.findByRole("heading", { name: "بخش مربی" });

    // The refresh 15 minutes later reads the roles again: the Owner stopped this trainer.
    act(() => siteSessionStore.signIn(siteAccess({ member: null, trainer: null })));

    expect(await screen.findByRole("alert")).toHaveTextContent("مربی‌گری شما تمام شده است.");
  });

  it("Home_SignOut_EndsTheSessionOnTheServerAndHere", async () => {
    const api = mockApi({
      "GET /api/site/me": () => json(200, siteAccess().me),
      "POST /api/site/auth/logout": () => new Response(null, { status: 204 }),
    });
    renderSite("/", { access: siteAccess() });

    fireEvent.click(await screen.findByRole("button", { name: "خروج" }));

    expect(await screen.findByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/logout")).toHaveLength(1);
  });
});
