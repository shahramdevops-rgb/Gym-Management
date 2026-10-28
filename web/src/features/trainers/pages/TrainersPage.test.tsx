import { fireEvent, screen, waitFor, within } from "@testing-library/react";

import type { Trainer } from "@/features/trainers/api";
import { formatDate, formatDateTime } from "@/lib/format";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
  type Handler,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const hamid: Trainer = {
  id: "0199a000-0000-7000-8000-0000000000c1",
  fullName: "حمید رستمی",
  phoneNumber: "+989121234567",
  isActive: true,
  periods: [
    { startedOn: "2026-03-01", endedOn: "2026-06-30" },
    { startedOn: "2026-10-01", endedOn: null },
  ],
  lastSignedInAt: "2026-10-27T15:10:00Z",
  version: 3,
};

const nazanin: Trainer = {
  id: "0199a000-0000-7000-8000-0000000000c2",
  fullName: "نازنین کاظمی",
  phoneNumber: "+989351112233",
  isActive: false,
  periods: [{ startedOn: "2026-05-01", endedOn: "2026-09-15" }],
  lastSignedInAt: null,
  version: 7,
};

function ownerWith(extra: Record<string, Handler> = {}) {
  return mockApi({
    ...signedInHandlers(owner),
    "GET /api/trainers": () => json(200, [hamid, nazanin]),
    ...extra,
  });
}

function boxOf(name: string) {
  return screen.getByRole("article", { name });
}

async function bodyOf(request: Request) {
  return (await request.json()) as Record<string, unknown>;
}

describe("TrainersPage", () => {
  it("TrainersPage_StaffUser_SeesNoAccessMessage", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp("/trainers", { session: session() });

    expect(await screen.findByText("اجازهٔ دسترسی به این بخش را ندارید.")).toBeInTheDocument();
    expect(api.requestsTo("GET", "/api/trainers")).toHaveLength(0);
  });

  it("TrainersPage_NoTrainers_SaysSo", async () => {
    ownerWith({ "GET /api/trainers": () => json(200, []) });

    renderApp("/trainers", { session: session() });

    expect(await screen.findByText("هنوز هیچ مربی‌ای ثبت نشده است.")).toBeInTheDocument();
  });

  it("TrainersPage_Trainers_ShowsEachAsABoxWithItsPeriodsAndLastSignIn", async () => {
    ownerWith();

    renderApp("/trainers", { session: session() });

    await screen.findByText("حمید رستمی");
    const active = boxOf("حمید رستمی");
    expect(within(active).getByText("مربی")).toBeInTheDocument();
    expect(within(active).getByText("۰۹۱۲ ۱۲۳ ۴۵۶۷")).toBeInTheDocument();
    expect(within(active).getByText(formatDate("2026-10-01"))).toBeInTheDocument();
    expect(within(active).getByText(formatDateTime(hamid.lastSignedInAt))).toBeInTheDocument();
    // The period before this one stays in the history with its dates (§15).
    expect(
      within(active).getByText(`${formatDate("2026-03-01")} تا ${formatDate("2026-06-30")}`),
    ).toBeInTheDocument();
    expect(within(active).getByRole("button", { name: "پایان مربی‌گری" })).toBeInTheDocument();

    const stopped = boxOf("نازنین کاظمی");
    expect(within(stopped).getByText("پایان‌یافته")).toBeInTheDocument();
    expect(within(stopped).getByText("هنوز وارد سایت نشده")).toBeInTheDocument();
    expect(within(stopped).getByRole("button", { name: "شروع دوباره" })).toBeInTheDocument();
  });

  it("CreateTrainer_ValidForm_SendsNormalizedValuesAndSaysSo", async () => {
    const api = ownerWith({
      "POST /api/trainers": () =>
        json(201, { ...hamid, id: "0199a000-0000-7000-8000-0000000000c3", fullName: "علی یوسفی" }),
    });
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(screen.getByRole("button", { name: "افزودن مربی" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی"), {
      target: { value: "  علی   يوسفی " },
    });
    fireEvent.change(within(dialog).getByLabelText("شماره موبایل"), {
      target: { value: "۰۹۱۲۷۷۷۸۸۹۹" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت مربی" }));

    expect(await within(dialog).findByText("مربی ثبت شد")).toBeInTheDocument();
    const [request] = api.requestsTo("POST", "/api/trainers");
    expect(await bodyOf(request!)).toEqual({ fullName: "علی یوسفی", phoneNumber: "09127778899" });
    // The list is asked again, so the new box shows.
    await waitFor(() => expect(api.requestsTo("GET", "/api/trainers")).toHaveLength(2));
  });

  it("CreateTrainer_EmptyForm_SaysWhatIsMissingAndSendsNothing", async () => {
    const api = ownerWith();
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(screen.getByRole("button", { name: "افزودن مربی" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت مربی" }));

    expect(await within(dialog).findByText("نام مربی را وارد کنید.")).toBeInTheDocument();
    expect(api.requestsTo("POST", "/api/trainers")).toHaveLength(0);
  });

  it("CreateTrainer_PhoneOfAnotherTrainer_ShowsItUnderThePhone", async () => {
    ownerWith({ "POST /api/trainers": () => problem(409, "Trainers.PhoneAlreadyExists") });
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(screen.getByRole("button", { name: "افزودن مربی" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText("نام و نام خانوادگی"), {
      target: { value: "علی یوسفی" },
    });
    fireEvent.change(within(dialog).getByLabelText("شماره موبایل"), {
      target: { value: "09351112233" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "ثبت مربی" }));

    expect(
      await within(dialog).findByText("این شماره موبایل قبلاً برای مربی دیگری ثبت شده است."),
    ).toBeInTheDocument();
  });

  it("EditTrainer_NewPhone_WarnsAndSendsTheVersion", async () => {
    const api = ownerWith({
      [`PUT /api/trainers/${hamid.id}`]: () =>
        json(200, { ...hamid, phoneNumber: "+989129998877", version: 4 }),
    });
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(within(boxOf("حمید رستمی")).getByRole("button", { name: "ویرایش" }));
    const dialog = await screen.findByRole("dialog");
    // Opened on the saved values; the same number is not a change.
    expect(within(dialog).getByLabelText("نام و نام خانوادگی")).toHaveValue("حمید رستمی");
    expect(within(dialog).queryByText(/با شمارهٔ تازه/)).not.toBeInTheDocument();

    fireEvent.change(within(dialog).getByLabelText("شماره موبایل"), {
      target: { value: "09129998877" },
    });
    expect(within(dialog).getByText(/مربی از سایت خارج می‌شود/)).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "ذخیره" }));

    expect(await within(dialog).findByText("تغییرات مربی ذخیره شد")).toBeInTheDocument();
    const [request] = api.requestsTo("PUT", `/api/trainers/${hamid.id}`);
    expect(await bodyOf(request!)).toEqual({
      fullName: "حمید رستمی",
      phoneNumber: "09129998877",
      version: 3,
    });
  });

  it("EditTrainer_ChangedByAnotherTab_SaysSo", async () => {
    ownerWith({
      [`PUT /api/trainers/${hamid.id}`]: () => problem(409, "Trainers.ChangedConcurrently"),
    });
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(within(boxOf("حمید رستمی")).getByRole("button", { name: "ویرایش" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "ذخیره" }));

    expect(await within(dialog).findByText(/این مربی هم‌زمان تغییر کرد/)).toBeInTheDocument();
  });

  it("StopTrainer_AsksFirstThenStopsAndSaysSo", async () => {
    const api = ownerWith({
      [`POST /api/trainers/${hamid.id}/stop`]: () => json(200, { ...hamid, isActive: false }),
    });
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(within(boxOf("حمید رستمی")).getByRole("button", { name: "پایان مربی‌گری" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent(/تا ۱۵ دقیقهٔ دیگر/);
    expect(api.requestsTo("POST", `/api/trainers/${hamid.id}/stop`)).toHaveLength(0);

    fireEvent.click(within(dialog).getByRole("button", { name: "بله، مربی‌گری تمام شود" }));

    expect(await screen.findByRole("status")).toHaveTextContent("مربی‌گری «حمید رستمی» تمام شد.");
    expect(api.requestsTo("POST", `/api/trainers/${hamid.id}/stop`)).toHaveLength(1);
  });

  it("StopTrainer_No_SendsNothing", async () => {
    const api = ownerWith();
    renderApp("/trainers", { session: session() });
    await screen.findByText("حمید رستمی");

    fireEvent.click(within(boxOf("حمید رستمی")).getByRole("button", { name: "پایان مربی‌گری" }));
    fireEvent.click(
      within(await screen.findByRole("dialog")).getByRole("button", { name: "خیر، برگرد" }),
    );

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(api.requestsTo("POST", `/api/trainers/${hamid.id}/stop`)).toHaveLength(0);
  });

  it("RestartTrainer_OneClick_StartsANewPeriod", async () => {
    const api = ownerWith({
      [`POST /api/trainers/${nazanin.id}/restart`]: () => json(200, { ...nazanin, isActive: true }),
    });
    renderApp("/trainers", { session: session() });
    await screen.findByText("نازنین کاظمی");

    fireEvent.click(within(boxOf("نازنین کاظمی")).getByRole("button", { name: "شروع دوباره" }));

    expect(await screen.findByRole("status")).toHaveTextContent("«نازنین کاظمی» دوباره مربی شد.");
    expect(api.requestsTo("POST", `/api/trainers/${nazanin.id}/restart`)).toHaveLength(1);
  });

  it("RestartTrainer_AlreadyActive_ShowsTheError", async () => {
    ownerWith({
      [`POST /api/trainers/${nazanin.id}/restart`]: () => problem(409, "Trainers.AlreadyActive"),
    });
    renderApp("/trainers", { session: session() });
    await screen.findByText("نازنین کاظمی");

    fireEvent.click(within(boxOf("نازنین کاظمی")).getByRole("button", { name: "شروع دوباره" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("این شخص هم‌اکنون مربی است.");
  });
});
