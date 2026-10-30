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
export function draftOf(session: Pick<Session, "start" | "end" | "capacity">): DraftSession {
  return toDraft({
    start: toMinutes(session.start),
    end: toMinutes(session.end),
    capacity: Number(session.capacity),
  });
}

/** What the API is sent: the drafts without their keys. */
export function inputsOf(drafts: readonly DraftSession[]): SessionInput[] {
  return drafts.map(({ start, end, capacity }) => ({ start, end, capacity }));
}
