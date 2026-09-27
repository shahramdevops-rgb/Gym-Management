import type { Prices } from "@/features/settings/api";

import { json } from "./mockApi";

/** Both prices set: a session costs 75,000, so the fixtures' 12-session plan costs 900,000. */
export const prices: Prices = {
  sessionPrice: 75000,
  singleVisitPrice: 150000,
  version: 7,
};

/** What a fresh database holds until the Owner opens settings (BUSINESS_RULES.md §3). */
export const pricesNotSet: Prices = {
  sessionPrice: null,
  singleVisitPrice: null,
  version: 1,
};

/** GET /api/pricing. */
export function pricesResponse(body: Prices = prices): Response {
  return json(200, body);
}
