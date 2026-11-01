import type { Session, SessionInput } from "./api";
import { toMinutes } from "./labels";

/** A session being edited, with a key that stays while its times change. */
export interface DraftSession extends SessionInput {
  key: string;
}

let lastKey = 0;

export function toDraft(session: SessionInput): DraftSession {
  lastKey += 1;
  return { ...session, key: String(lastKey) };
}

/** A session from the API, ready to edit. */
export function draftOf(
  session: Pick<Session, "start" | "end" | "capacity" | "isVip">,
): DraftSession {
  return toDraft({
    start: toMinutes(session.start),
    end: toMinutes(session.end),
    capacity: Number(session.capacity),
    isVip: session.isVip,
  });
}

/** What the API is sent: the drafts without their keys. */
export function inputsOf(drafts: readonly DraftSession[]): SessionInput[] {
  return drafts.map(({ start, end, capacity, isVip }) => ({ start, end, capacity, isVip }));
}

/**
 * What is wrong with each session of one day, by its key: the API's own checks of a day
 * (BUSINESS_RULES.md §15 *The trainer's sessions*), made here too so the box to fix says so before
 * anything is sent. The API still has the last word.
 */
export function sessionProblems(sessions: readonly DraftSession[]): Map<string, string> {
  const problems = new Map<string, string>();
  sessions.forEach((session, index) => {
    if (session.end <= session.start) {
      problems.set(session.key, "ساعت پایان باید بعد از ساعت شروع باشد.");
    } else if (
      sessions
        .slice(0, index)
        .some((other) => other.start === session.start && other.end === session.end)
    ) {
      problems.set(session.key, "سانسی با همین ساعت شروع و پایان در این روز هست.");
    }
  });
  return problems;
}

/** Brings the first session with a problem into view: on a long week it may be far above. */
export function showFirstProblem() {
  requestAnimationFrame(() =>
    document
      .querySelector("[data-session-problem]")
      ?.scrollIntoView?.({ block: "center", behavior: "smooth" }),
  );
}
