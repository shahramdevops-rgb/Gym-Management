/**
 * A new phone's way from its code to the sign-up screen: the ticket the sign-in answered and the
 * phone it proves (BUSINESS_RULES.md §15, 15 minutes). In memory only, like the access token: a
 * reload loses it, and the sign-up screen then sends the person back for a new code, which is
 * what the API would ask for anyway once the 15 minutes are over.
 */
export interface SignUpTicket {
  phoneNumber: string;
  ticket: string;
  expiresAt: string;
}

let current: SignUpTicket | undefined;

export const signUpTicketStore = {
  get: (): SignUpTicket | undefined => current,
  set: (ticket: SignUpTicket) => {
    current = ticket;
  },
  clear: () => {
    current = undefined;
  },
};
