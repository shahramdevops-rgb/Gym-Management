import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/**
 * The ways a customer can reach the shop (BUSINESS_RULES.md §16 *The shop admin*): one set for the
 * whole shop. Each is `null` until the shop admin gives it.
 */
export type ShopContacts = components["schemas"]["ShopContactsResponse"];

export type ShopContactsInput = components["schemas"]["UpdateShopContactsCommand"];

export const shopKeys = {
  all: ["shop"] as const,
  contacts: () => [...shopKeys.all, "contacts"] as const,
};

/** The shop admin edits them; the Owner reads them (§16). */
export function useShopContacts() {
  return useQuery({
    queryKey: shopKeys.contacts(),
    queryFn: async () => {
      const { data, error } = await api.GET("/api/shop/contacts");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** The shop admin only; the API refuses the Owner. */
export function useUpdateShopContacts() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: ShopContactsInput) => {
      const { data, error } = await api.PUT("/api/shop/contacts", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (contacts) => {
      queryClient.setQueryData(shopKeys.contacts(), contacts);
    },
  });
}
