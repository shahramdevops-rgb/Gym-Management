import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/** The SMS settings page (BUSINESS_RULES.md §10 *SMS settings*): empty and off until the Owner fills it. */
export type SmsSettings = components["schemas"]["SmsSettingsResponse"];

/** One kind's row: on/off, its number, its send time (`HH:mm:ss`) and its Kavenegar template. */
export type SmsKindSettings = components["schemas"]["SmsKindSettings"];

export type SmsSettingsInput = components["schemas"]["UpdateSmsSettingsCommand"];

/** The SMS account's credit in Toman; `remainingToman` is null in test mode or when Kavenegar does not answer. */
export type SmsCredit = components["schemas"]["SmsCreditResponse"];

export const smsSettingsKeys = {
  all: ["sms-settings"] as const,
};

export const smsCreditKeys = {
  all: ["sms-credit"] as const,
};

/** Asks Kavenegar each time the page opens (BUSINESS_RULES.md §10 *Sending*): free, and the page is opened rarely. */
export function useSmsCredit() {
  return useQuery({
    queryKey: smsCreditKeys.all,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/credit");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Owner only, like every SMS page: every SMS costs money, and the Owner is the one who pays. */
export function useSmsSettings() {
  return useQuery({
    queryKey: smsSettingsKeys.all,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/settings");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

export function useUpdateSmsSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: SmsSettingsInput) => {
      const { data, error } = await api.PUT("/api/sms/settings", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSuccess: (settings) => {
      queryClient.setQueryData(smsSettingsKeys.all, settings);
    },
  });
}
