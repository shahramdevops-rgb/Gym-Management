import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/** The SMS settings page (BUSINESS_RULES.md §10 *SMS settings*): empty and off until the Owner fills it. */
export type SmsSettings = components["schemas"]["SmsSettingsResponse"];

/** One kind's row: on/off, its number and its send time (`HH:mm:ss`). */
export type SmsKindSettings = components["schemas"]["SmsKindSettings"];

export type SmsSettingsInput = components["schemas"]["UpdateSmsSettingsCommand"];

/**
 * The SMS account's credit in Toman; `remainingToman` is null in test mode or when Kavenegar does
 * not answer. `creditUsedUp` is the warning: the latest failure for credit is newer than the latest
 * message sent (BUSINESS_RULES.md §10 *The credit warning*).
 */
export type SmsCredit = components["schemas"]["SmsCreditResponse"];

/** One SMS in the history: what was sent, to whom, and how it went. */
export type SmsMessage = components["schemas"]["SmsMessageResponse"];

export type SmsMessageKind = components["schemas"]["NotificationKind"];

export type SmsMessageStatus = components["schemas"]["NotificationStatus"];

export interface SmsMessageFilter {
  /** ISO dates, both inclusive: the day each message was written, in the gym's time zone. */
  from?: string;
  to?: string;
  kind?: SmsMessageKind;
  status?: SmsMessageStatus;
  page: number;
}

export const smsMessagesPageSize = 20;

export const smsMessageKeys = {
  all: ["sms-messages"] as const,
  list: (filter: SmsMessageFilter) => [...smsMessageKeys.all, filter] as const,
};

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

/** The SMS history (پیامک‌ها), the latest first, with what the filter's messages cost in all. */
export function useSmsMessages(filter: SmsMessageFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: smsMessageKeys.list(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/messages", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            Kind: filter.kind,
            Status: filter.status,
            Page: filter.page,
            PageSize: smsMessagesPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        totalCostToman: data.totalCostToman,
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / smsMessagesPageSize)),
      };
    },
  });
}

/**
 * Resends a failed or unknown SMS: exactly the same message, one request (BUSINESS_RULES.md §10
 * *Sending*). Its outcome changes the history's rows and total, and may clear the credit warning.
 */
export function useResendSms() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      const { data, error } = await api.POST("/api/sms/messages/{id}/resend", {
        params: { path: { id } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: smsMessageKeys.all });
      void queryClient.invalidateQueries({ queryKey: smsCreditKeys.all });
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
