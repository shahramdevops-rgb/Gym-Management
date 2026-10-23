/**
 * The broadcast's text as the server writes it (BUSINESS_RULES.md §10 *The broadcast*), so the page
 * counts the characters and parts the way the send will check them. The same rules as `SmsBroadcast`
 * and `SmsText.ForBroadcast` in the backend, with the same cases in the tests.
 */

/** A Persian SMS holds 70 characters in one part and 67 in each part of a longer one. */
export const singlePartLength = 70;
export const multiPartLength = 67;

/** The most parts any recipient's message may take (decided with the developer). */
export const broadcastMaxParts = 3;

/** Kavenegar's opt-out line, on a line of its own at the end of every message. */
const optOut = "لغو11";

/**
 * Trimmed, every line break one `\n`: a browser's textarea gives `\r\n` on some systems, which
 * would count two characters on the phone for one break.
 */
export function normalizeBroadcastText(text: string): string {
  return text.replace(/\r\n?/g, "\n").trim();
}

/** The whole message to one member: their first name, the Owner's text, the opt-out line. */
export function broadcastMessage(firstName: string, text: string): string {
  return `${firstName} عزیز، ${normalizeBroadcastText(text)}\n${optOut}`;
}

/**
 * How many parts a text takes. Characters are UTF-16 code units, as `String.length` counts them and
 * as the SMS is sent: an emoji takes two.
 */
export function smsParts(text: string): number {
  return text.length <= singlePartLength ? 1 : Math.ceil(text.length / multiPartLength);
}

/** The most characters the Owner's text may have with this first name in front of it. */
export function broadcastMaxTextLength(longestFirstName: string): number {
  return broadcastMaxParts * multiPartLength - broadcastMessage(longestFirstName, "").length;
}
