import { screen, waitFor } from "@testing-library/react";

import { paths } from "@/app/paths";
import { smsCreditRefreshMs, type SmsCredit } from "@/features/sms/api";
import {
  json,
  mockApi,
  owner,
  problem,
  session,
  signedInHandlers,
  staffUser,
} from "@/test/mockApi";
import { renderApp } from "@/test/renderApp";

const creditPath = "/api/sms/credit";

function credit(remainingToman: number | null): SmsCredit {
  return { isTestMode: false, remainingToman, creditUsedUp: false };
}

/** The Owner's header answering the credit with `answer`, which a test may change between asks. */
function creditAnswers(answer: { current: () => Response }) {
  return { ...signedInHandlers(owner), [`GET ${creditPath}`]: () => answer.current() };
}

function withCredit(remainingToman: number | null) {
  return creditAnswers({ current: () => json(200, credit(remainingToman)) });
}

async function findBadge() {
  return screen.findByRole("link", { name: /^اعتبار پیامک/ });
}

describe("SmsCreditBadge", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("SmsCreditBadge_BelowOneHundredThousand_IsRedBlinkingInTheHeaderAndOpensTheSettings", async () => {
    mockApi(withCredit(95_000));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge.closest("header")).not.toBeNull();
    expect(badge).toHaveTextContent("۹۵٬۰۰۰ تومان");
    expect(badge).toHaveAccessibleName(/کم است/);
    expect(badge.className).toContain("bg-destructive/10");
    expect(badge.querySelector(".motion-safe\\:animate-ping")).not.toBeNull();
    expect(badge).toHaveAttribute("href", paths.smsSettings);
    expect(badge.className).not.toContain("animate-credit-blink");
  });

  it("SmsCreditBadge_BelowOneThousand_TheWholePillFlashes", async () => {
    mockApi(withCredit(800));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge).toHaveTextContent("۸۰۰ تومان");
    expect(badge).toHaveAccessibleName(/تقریباً تمام شده/);
    expect(badge.className).toContain("animate-credit-blink");
    expect(badge.className).toContain("motion-reduce:animate-none");
    // The pill itself flashes now, so the icon's small ring is not needed.
    expect(badge.querySelector(".motion-safe\\:animate-ping")).toBeNull();
  });

  it("SmsCreditBadge_ExactlyOneThousand_IsRedWithoutTheFlash", async () => {
    mockApi(withCredit(1_000));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge.className).not.toContain("animate-credit-blink");
    expect(badge.querySelector(".motion-safe\\:animate-ping")).not.toBeNull();
  });

  it.each([100_000, 200_000])(
    "SmsCreditBadge_FromOneToTwoHundredThousand_IsOrange (%d)",
    async (remainingToman) => {
      mockApi(withCredit(remainingToman));

      renderApp(paths.changePassword, { session: session() });

      const badge = await findBadge();
      expect(badge.className).toContain("bg-warning/10");
      expect(badge.querySelector(".motion-safe\\:animate-ping")).toBeNull();
    },
  );

  it("SmsCreditBadge_AboveTwoHundredThousand_IsGreen", async () => {
    mockApi(withCredit(200_001));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge).toHaveTextContent("۲۰۰٬۰۰۱ تومان");
    expect(badge.className).toContain("bg-success/10");
  });

  it("SmsCreditBadge_TestMode_ShowsNothing", async () => {
    const api = mockApi(signedInHandlers(owner));

    renderApp(paths.changePassword, { session: session() });

    await waitFor(() => expect(api.requestsTo("GET", creditPath)).toHaveLength(1));
    expect(screen.queryByRole("link", { name: /^اعتبار پیامک/ })).not.toBeInTheDocument();
  });

  it("SmsCreditBadge_KavenegarNeverAnswered_IsGreyAndUnknown", async () => {
    mockApi(withCredit(null));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge).toHaveTextContent("نامعلوم");
    expect(badge.className).toContain("bg-muted");
  });

  it("SmsCreditBadge_KavenegarStopsAnswering_KeepsTheLastFigureInGrey", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const answer = { current: () => json(200, credit(150_000)) };
    mockApi(creditAnswers(answer));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge.className).toContain("bg-warning/10");

    answer.current = () => json(200, credit(null));
    await vi.advanceTimersByTimeAsync(smsCreditRefreshMs);

    await waitFor(() => expect(badge.className).toContain("bg-muted"));
    expect(badge).toHaveTextContent("۱۵۰٬۰۰۰ تومان");
    expect(badge).toHaveAccessibleName(/آخرین عدد/);
  });

  it("SmsCreditBadge_RequestFails_KeepsTheLastFigureInGrey", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const answer = { current: () => json(200, credit(80_000)) };
    mockApi(creditAnswers(answer));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge.className).toContain("bg-destructive/10");

    answer.current = () => problem(500, "Server.Error");
    await vi.advanceTimersByTimeAsync(smsCreditRefreshMs);

    await waitFor(() => expect(badge.className).toContain("bg-muted"), { timeout: 5000 });
    expect(badge).toHaveTextContent("۸۰٬۰۰۰ تومان");
  });

  it("SmsCreditBadge_AsksAgainEveryTenMinutes_AndTheColourFollows", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const answer = { current: () => json(200, credit(90_000)) };
    const api = mockApi(creditAnswers(answer));

    renderApp(paths.changePassword, { session: session() });

    const badge = await findBadge();
    expect(badge.className).toContain("bg-destructive/10");

    // The Owner charged the account on Kavenegar's own site.
    answer.current = () => json(200, credit(500_000));
    await vi.advanceTimersByTimeAsync(smsCreditRefreshMs);

    await waitFor(() => expect(badge.className).toContain("bg-success/10"));
    expect(badge).toHaveTextContent("۵۰۰٬۰۰۰ تومان");
    expect(api.requestsTo("GET", creditPath)).toHaveLength(2);
  });

  it("SmsCreditBadge_Staff_NeverAsks", async () => {
    const api = mockApi(signedInHandlers(staffUser));

    renderApp(paths.changePassword, { session: session() });

    await screen.findByText(staffUser.fullName);
    expect(api.requestsTo("GET", creditPath)).toHaveLength(0);
    expect(api.unexpected).toEqual([]);
  });
});
