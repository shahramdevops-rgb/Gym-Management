import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Expense = components["schemas"]["ExpenseResponse"];
export type ExpenseCategory = components["schemas"]["ExpenseCategoryResponse"];

export const expensesPageSize = 20;

/** docs/ARCHITECTURE.md: the API returns at most 100 rows per page. */
const maxPageSize = 100;

/** BUSINESS_RULES.md §9, mirroring Expense and ExpenseCategory. */
export const expenseLimits = {
  categoryNameMaxLength: 100,
  descriptionMaxLength: 500,
  referenceNumberMaxLength: 100,
  voidReasonMaxLength: 500,
} as const;

/**
 * Query keys. Every key starts with "expenses", so one invalidation after any change refreshes
 * the list, its total and the category picker together — a rename shows on every row, because
 * an expense carries its category's current name rather than a copy.
 */
export const expenseKeys = {
  all: ["expenses"] as const,
  categories: () => [...expenseKeys.all, "categories"] as const,
  list: (filter: ExpenseListFilter) => [...expenseKeys.all, "list", filter] as const,
};

/** Every category, by name: the filter, the form's picker and the categories card. */
export function useExpenseCategories() {
  return useQuery({
    queryKey: expenseKeys.categories(),
    queryFn: async () => {
      // A gym has a dozen headings; the loop still reads on rather than stopping at a hundred.
      const items: ExpenseCategory[] = [];
      for (let page = 1; ; page++) {
        const { data, error } = await api.GET("/api/expenses/categories", {
          params: { query: { Page: page, PageSize: maxPageSize } },
        });
        if (error !== undefined) {
          throw error;
        }
        items.push(...data.items);
        if (data.items.length === 0 || items.length >= Number(data.totalCount)) {
          return items;
        }
      }
    },
  });
}

export interface ExpenseListFilter {
  /** ISO business dates, inclusive. Omitted: no bound. */
  from?: string;
  to?: string;
  /** Omitted: every category. */
  categoryId?: string;
  page: number;
}

/**
 * One page of expenses, newest first, voided ones included and marked, with the total of every
 * expense the filter matches across all pages — voided ones never counted (BUSINESS_RULES.md §9).
 */
export function useExpenseList(filter: ExpenseListFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: expenseKeys.list(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/expenses", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            CategoryId: filter.categoryId,
            Page: filter.page,
            PageSize: expensesPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return {
        items: data.items,
        totalCount: Number(data.totalCount),
        totalAmount: data.totalAmount,
        pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / expensesPageSize)),
      };
    },
  });
}

function useExpenseMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: expenseKeys.all });
    },
  });
}

export interface ExpenseInput {
  /** The plain decimal string the API reads (`normalizeMoney`), never a float. */
  amount: string;
  categoryId: string;
  /** ISO business date, not after the gym's today. */
  expenseDate: string;
  description: string;
  referenceNumber: string | null;
}

export function useRecordExpense() {
  return useExpenseMutation(async (body: ExpenseInput) => {
    const { data, error } = await api.POST("/api/expenses", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUpdateExpense() {
  return useExpenseMutation(
    async ({ id, ...body }: ExpenseInput & { id: string; version: number | string }) => {
      const { data, error } = await api.PUT("/api/expenses/{id}", {
        params: { path: { id } },
        body,
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}

export function useVoidExpense() {
  return useExpenseMutation(async ({ id, reason }: { id: string; reason: string }) => {
    const { data, error } = await api.POST("/api/expenses/{id}/void", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useCreateExpenseCategory() {
  return useExpenseMutation(async (name: string) => {
    const { data, error } = await api.POST("/api/expenses/categories", { body: { name } });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useRenameExpenseCategory() {
  return useExpenseMutation(
    async ({ id, name, version }: { id: string; name: string; version: number | string }) => {
      const { data, error } = await api.PUT("/api/expenses/categories/{id}", {
        params: { path: { id } },
        body: { name, version },
      });
      if (error !== undefined) {
        throw error;
      }
      return data;
    },
  );
}
