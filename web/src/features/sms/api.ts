import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/** The SMS settings page (BUSINESS_RULES.md §10 *SMS settings*): empty and off until the Owner fills it. */
export type SmsSettings = components["schemas"]["SmsSettingsResponse"];

/** One kind's row: on/off, its number and its send time (`HH:mm:ss`). */
export type SmsKindSettings = components["schemas"]["SmsKindSettings"];

export type SmsSettingsInput = components["schemas"]["UpdateSmsSettingsCommand"];

/** One kind's message as it reaches the phone, with a sample member, and its SMS parts. */
export type SmsSample = components["schemas"]["SmsSample"];

/** Every kind with a fixed wording, built by the API's own `SmsText` (task 10.15). */
export type SmsSamples = components["schemas"]["SmsSamplesResponse"];

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
  /** One broadcast's messages only, opened from «پیامک گروهی». */
  broadcastId?: string;
  page: number;
}

/** One broadcast as «پیامک گروهی» lists it (BUSINESS_RULES.md §10 *The broadcast*). */
export type Broadcast = components["schemas"]["BroadcastResponse"];

export type BroadcastAudience = components["schemas"]["BroadcastAudience"];

export type BroadcastStatus = components["schemas"]["BroadcastStatus"];

export type BroadcastStopReason = NonNullable<components["schemas"]["BroadcastStopReason"]>;

/** Both groups the Owner can choose from: how many are in each and the longest first name. */
export type BroadcastAudiences = components["schemas"]["BroadcastAudiencesResponse"];

/** The confirmation's figures: recipients, parts, the estimated cost and the longest message. */
export type BroadcastPreview = components["schemas"]["BroadcastPreviewResponse"];

export interface BroadcastInput {
  text: string;
  audience: BroadcastAudience;
}

export const smsMessagesPageSize = 20;

export const smsMessageKeys = {
  all: ["sms-messages"] as const,
  list: (filter: SmsMessageFilter) => [...smsMessageKeys.all, filter] as const,
};

export const smsSettingsKeys = {
  all: ["sms-settings"] as const,
};

export const smsSampleKeys = {
  all: ["sms-samples"] as const,
};

export const smsCreditKeys = {
  all: ["sms-credit"] as const,
};

export const broadcastKeys = {
  all: ["sms-broadcasts"] as const,
  list: (page: number) => [...broadcastKeys.all, "list", page] as const,
  audiences: () => [...broadcastKeys.all, "audiences"] as const,
};

export const broadcastsPageSize = 20;

/** How often the header asks Kavenegar again (BUSINESS_RULES.md §10 *The credit in the header*). */
export const smsCreditRefreshMs = 10 * 60 * 1000;

/**
 * The header's thresholds in Toman: the whole pill flashing below the first, red below the second,
 * orange up to the third, green above.
 */
export const smsCreditEmptyToman = 1_000;
export const smsCreditLowToman = 100_000;
export const smsCreditFairToman = 200_000;

export interface SmsCreditState extends SmsCredit {
  /**
   * The latest figure Kavenegar gave in this tab: `remainingToman` while it answers, the one before
   * when it does not, null when it never has.
   */
  lastKnownToman: SmsCredit["remainingToman"];
}

/**
 * Asks Kavenegar each time a page using it opens (BUSINESS_RULES.md §10 *Sending*): free. One cache
 * entry for the SMS pages and the header, so the header's figure follows a resend too.
 */
export function useSmsCredit({ refetchInterval }: { refetchInterval?: number } = {}) {
  const queryClient = useQueryClient();

  return useQuery({
    queryKey: smsCreditKeys.all,
    refetchInterval,
    queryFn: async (): Promise<SmsCreditState> => {
      const { data, error } = await api.GET("/api/sms/credit");
      if (error !== undefined) {
        throw error;
      }
      const previous = queryClient.getQueryData<SmsCreditState>(smsCreditKeys.all);
      return { ...data, lastKnownToman: data.remainingToman ?? previous?.lastKnownToman ?? null };
    },
  });
}

/**
 * The site's sign-in code SMS (BUSINESS_RULES.md §15 *Trainers*, «The code SMS in the panel»):
 * today's count of the gym's 300, and this Jalali month's count and cost. Never a code or a phone.
 */
export type SignInCodeSms = components["schemas"]["SignInCodeSmsResponse"];

export const signInCodeSmsKeys = {
  all: ["sms-sign-in-codes"] as const,
};

export function useSignInCodeSms() {
  return useQuery({
    queryKey: signInCodeSmsKeys.all,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/sign-in-codes");
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
            BroadcastId: filter.broadcastId,
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

/**
 * The sample texts beside each kind on «تنظیمات پیامک». They change only with a release (and the
 * dates with the day), so they are asked once per visit.
 */
export function useSmsSamples() {
  return useQuery({
    queryKey: smsSampleKeys.all,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/samples");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    staleTime: Infinity,
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

/** The two groups as they stand now, read when «پیامک گروهی» opens. */
export function useBroadcastAudiences() {
  return useQuery({
    queryKey: broadcastKeys.audiences(),
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/broadcasts/audiences");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/**
 * The broadcasts, the latest first. While one is sending it is asked again every few seconds, so
 * the Owner sees it go out and end without reloading.
 */
export function useBroadcasts(page: number) {
  return useQuery({
    queryKey: broadcastKeys.list(page),
    placeholderData: keepPreviousData,
    refetchInterval: (query) =>
      query.state.data?.items.some((broadcast) => broadcast.status === "Sending") === true
        ? 3000
        : false,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/sms/broadcasts", {
        params: { query: { Page: page, PageSize: broadcastsPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / broadcastsPageSize)),
      };
    },
  });
}

/** The confirmation's figures for a text and a group; writes nothing. */
export function usePreviewBroadcast() {
  return useMutation({
    mutationFn: async (body: BroadcastInput) => {
      const { data, error } = await api.POST("/api/sms/broadcasts/preview", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}

/** Sends a broadcast: saved at once, its messages written and sent in the background. */
export function useSendBroadcast() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: BroadcastInput) => {
      const { data, error } = await api.POST("/api/sms/broadcasts", { body });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: broadcastKeys.all });
      void queryClient.invalidateQueries({ queryKey: smsMessageKeys.all });
    },
  });
}

/** «ادامه ارسال»: a stopped broadcast goes on to the members it has not reached. */
export function useResumeBroadcast() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: string) => {
      const { data, error } = await api.POST("/api/sms/broadcasts/{id}/resume", {
        params: { path: { id } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: broadcastKeys.all });
      void queryClient.invalidateQueries({ queryKey: smsMessageKeys.all });
      void queryClient.invalidateQueries({ queryKey: smsCreditKeys.all });
    },
  });
}
