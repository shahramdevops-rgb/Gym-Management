import { fireEvent, screen } from "@testing-library/react";

import { chooseBirthDate } from "@/test/jalaliDate";
import { json, mockApi, problem } from "@/test/mockApi";

import { siteSessionStore } from "../session";
import { siteAccess, renderSite } from "../../test/renderSite";
import { signUpTicketStore, type SignUpTicket } from "../signUpTicket";

function ticket(): SignUpTicket {
  return {
    phoneNumber: "09121234567",
    ticket: "ticket-1",
    expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
  };
}

function fillAndSubmit(fullName: string, birthDate: string) {
  fireEvent.change(screen.getByLabelText("نام و نام خانوادگی"), { target: { value: fullName } });
  chooseBirthDate(birthDate);
  fireEvent.click(screen.getByRole("button", { name: "ثبت‌نام" }));
}

describe("SignUpPage", () => {
  it("SignUp_NoCodeJustBefore_IsSentToSignIn", () => {
    mockApi({});

    renderSite("/sign-up");

    expect(screen.getByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
  });

  it("SignUp_EmptyForm_ShowsTheDesksPersianErrorsWithoutCallingTheApi", async () => {
    const api = mockApi({});
    renderSite("/sign-up", { signUpTicket: ticket() });

    fireEvent.click(screen.getByRole("button", { name: "ثبت‌نام" }));

    expect(await screen.findByText("نام و نام خانوادگی را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/site/auth/sign-up")).toHaveLength(0);
  });

  it("SignUp_Valid_SendsTheTicketNameAndGregorianDateAndOpensHome", async () => {
    const api = mockApi({
      "POST /api/site/auth/sign-up": () => json(200, siteAccess()),
      "GET /api/site/me": () => json(200, siteAccess().me),
    });
    renderSite("/sign-up", { signUpTicket: ticket() });

    fillAndSubmit("علی محمدی", "1374/01/01");

    expect(await screen.findByRole("heading", { name: "سلام علی محمدی" })).toBeInTheDocument();
    expect(await api.requestsTo("POST", "/api/site/auth/sign-up")[0]!.json()).toEqual({
      ticket: "ticket-1",
      fullName: "علی محمدی",
      birthDate: "1995-03-21",
    });
    expect(signUpTicketStore.get()).toBeUndefined();
  });

  it("SignUp_TicketExpired_OffersANewCode", async () => {
    mockApi({ "POST /api/site/auth/sign-up": () => problem(422, "SiteAuth.SignUpExpired") });
    renderSite("/sign-up", { signUpTicket: ticket() });

    fillAndSubmit("علی محمدی", "1374/01/01");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "زمان ثبت‌نام تمام شد. دوباره کد بگیرید.",
    );
    fireEvent.click(screen.getByRole("link", { name: "دریافت کد تازه" }));
    expect(await screen.findByRole("heading", { name: "ورود یا ثبت‌نام" })).toBeInTheDocument();
  });

  it("SignUp_SomeoneSignsInOrOutMeanwhile_TicketIsDropped", () => {
    signUpTicketStore.set(ticket());

    siteSessionStore.signOut();

    expect(signUpTicketStore.get()).toBeUndefined();
  });
});
