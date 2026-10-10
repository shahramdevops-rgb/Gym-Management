import { keepPreviousData, useQuery } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/**
 * One row of «گزارش تغییرات»: who did what to which record, when, from where, the member it
 * belongs to and the changed fields (BUSINESS_RULES.md §11 *The audit screen*).
 */
export type AuditLog = components["schemas"]["AuditLogResponse"];

/** One field's value before and after, exactly as the log stored it; `labels.ts` formats it. */
export type AuditChange = components["schemas"]["AuditChangeResponse"];

export type AuditAction = components["schemas"]["AuditAction"];

/** An account that may appear in the log: the Owner and every staff member, deactivated included. */
export type AuditUser = components["schemas"]["UserFullName"];

export interface AuditLogFilter {
  /** ISO dates, both inclusive: the day each change happened, in the gym's time zone. */
  from?: string;
  to?: string;
  userId?: string;
  /** Only the rows with no user: logins, seeding, background jobs. */
  systemOnly?: boolean;
  entityType?: string;
  /** One record's history, with `entityType`. */
  entityId?: string;
  action?: AuditAction;
  memberId?: string;
  /** Also the refresh token and trusted device rows, hidden by default. */
  includeSignIns?: boolean;
  page: number;
}

export const auditLogsPageSize = 20;

export const auditLogKeys = {
  all: ["audit-logs"] as const,
  list: (filter: AuditLogFilter) => [...auditLogKeys.all, filter] as const,
  users: ["audit-logs", "users"] as const,
};

/** The audit log, the latest first (Owner only). */
export function useAuditLogs(filter: AuditLogFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: auditLogKeys.list(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/audit-logs", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            UserId: filter.userId,
            SystemOnly: filter.systemOnly || undefined,
            EntityType: filter.entityType,
            EntityId: filter.entityId,
            Action: filter.action,
            MemberId: filter.memberId,
            IncludeSignIns: filter.includeSignIns || undefined,
            Page: filter.page,
            PageSize: auditLogsPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / auditLogsPageSize)),
      };
    },
  });
}

/**
 * Every account, for the "who" filter and to name the users a change records. A handful of rows
 * that change rarely, so it is fetched once per visit.
 */
export function useAuditUsers() {
  return useQuery({
    queryKey: auditLogKeys.users,
    staleTime: Infinity,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/audit-logs/users");
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  });
}
