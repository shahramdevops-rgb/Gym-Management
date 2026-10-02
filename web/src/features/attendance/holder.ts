/**
 * Who a visit belongs to: a member, linked to their profile, or a guest, by the name the desk typed
 * (BUSINESS_RULES.md §7 *Guest visit*). Every screen that shows a visit reads it through here, so
 * none forgets that a visit may have no member.
 */
export interface DeskHolder {
  /** The member's id; `null` for a guest, who has no profile to link to. */
  id: string | null;
  fullName: string;
}

/** The fields a visit row carries about its holder, on the board, the map and a locker's history. */
interface HolderFields {
  memberId: string | null;
  memberFullName: string | null;
  guestName: string | null;
}

/** «مهمان»: the word beside a guest's name wherever a member would have a link or sessions. */
export const guestLabel = "مهمان";

/**
 * «فقط هوازی»: the mark on a member's visit that consumed no session (BUSINESS_RULES.md §7
 * *Cardio-only visit*), on the board, the map's box and every history.
 */
export const cardioOnlyLabel = "فقط هوازی";

/** A guest's visit: no member, a typed name. */
export function isGuestVisit(visit: { memberId: string | null }): boolean {
  return visit.memberId === null;
}

/** The name to show for a visit: the member's, or the guest's. */
export function holderName(visit: HolderFields): string {
  return visit.memberFullName ?? visit.guestName ?? "";
}

export function holderOf(visit: HolderFields): DeskHolder {
  return { id: visit.memberId, fullName: holderName(visit) };
}
