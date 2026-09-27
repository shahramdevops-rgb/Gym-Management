import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/** The gym's two prices (BUSINESS_RULES.md §3 *Prices*). Either is `null` until the Owner sets it. */
export type Prices = components["schemas"]["PricesResponse"];

export const priceKeys = {
  all: ["prices"] as const,
};

/**
 * The prices the desk sells at. Everyone reads them — the sale form shows the price before it is
 * confirmed, and the single-visit offer shows its price on the button — but only the Owner changes
 * them, on the settings screen.
 */
export function usePrices() {
  return useQuery({
    queryKey: priceKeys.all,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/pricing");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/**
 * What the settings form sends. Prices are decimal strings (`"150000.50"`), never JavaScript
 * numbers, for the same reason as every amount in the app: the API reads a JSON string into its
 * `decimal` without passing through floating point.
 */
export interface PricesInput {
  sessionPrice: string;
  singleVisitPrice: string;
  /** The `version` the form was filled from; if someone saved since, the API refuses. */
  version: Prices["version"];
}

export function useUpdatePrices() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: PricesInput) => {
      const { data, error } = await api.PUT("/api/pricing", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (prices) => {
      queryClient.setQueryData(priceKeys.all, prices);
    },
  });
}
