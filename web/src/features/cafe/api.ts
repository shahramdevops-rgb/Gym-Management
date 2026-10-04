import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { attendanceKeys, currentlyInsideRefetchMs } from "@/features/attendance/api";
import { lockerKeys } from "@/features/lockers/api";
import { memberKeys } from "@/features/members/api";
import { paymentKeys, type PaymentMethod } from "@/features/payments/api";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type ProductCategory = components["schemas"]["ProductCategoryResponse"];
export type Product = components["schemas"]["ProductResponse"];
export type CafeOrder = components["schemas"]["CafeOrderResponse"];
export type CafeOrderItem = components["schemas"]["CafeOrderItemResponse"];

export const productsPageSize = 20;
export const cafeOrdersPageSize = 20;

/** docs/ARCHITECTURE.md: the API returns at most 100 rows per page. */
const maxPageSize = 100;

/** BUSINESS_RULES.md §8, mirroring CafeOrderItem.MaxQuantity and CafeOrder.MaxItems. */
export const cafeLimits = {
  maxQuantity: 999,
  maxItems: 50,
  nameMaxLength: 100,
  cancelReasonMaxLength: 500,
} as const;

/**
 * Query keys. Every key starts with "cafe", so one invalidation after a change to the price list
 * refreshes the till, the management screen and the category picker together.
 */
export const cafeKeys = {
  all: ["cafe"] as const,
  categories: () => [...cafeKeys.all, "categories"] as const,
  products: (filter: ProductListFilter) => [...cafeKeys.all, "products", filter] as const,
  sellable: () => [...cafeKeys.all, "sellable"] as const,
  orders: (filter: CafeOrderListFilter) => [...cafeKeys.all, "orders", filter] as const,
  memberOrders: (memberId: string, page: number) =>
    [...cafeKeys.all, "memberOrders", memberId, page] as const,
  visitOrders: (attendanceId: string) => [...cafeKeys.all, "visitOrders", attendanceId] as const,
};

interface Paged<T> {
  items: T[];
  totalCount: number | string;
}

function toPage<T>(data: Paged<T>, pageSize: number) {
  return {
    items: data.items,
    totalCount: Number(data.totalCount),
    pageCount: Math.max(1, Math.ceil(Number(data.totalCount) / pageSize)),
  };
}

/**
 * Every row of a list, page after page. Used where the whole list has to be on screen at once —
 * the till's grid and the category picker — and a cafe's price list is a few dozen lines, so this
 * is normally one request. It still reads on rather than silently stopping at the first hundred.
 */
async function fetchAll<T>(fetchPage: (page: number) => Promise<Paged<T>>): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page++) {
    const data = await fetchPage(page);
    items.push(...data.items);
    if (data.items.length === 0 || items.length >= Number(data.totalCount)) {
      return items;
    }
  }
}

/** Every category, switched on or off: the management screen and the product form's picker. */
export function useProductCategories() {
  return useQuery({
    queryKey: cafeKeys.categories(),
    queryFn: () =>
      fetchAll(async (page) => {
        const { data, error } = await api.GET("/api/cafe/categories", {
          params: { query: { Page: page, PageSize: maxPageSize } },
        });
        if (error !== undefined) {
          throw error;
        }
        return data;
      }),
  });
}

export interface ProductListFilter {
  /** Omitted: every category. */
  categoryId?: string;
  page: number;
}

/** The management list: everything, sellable or not, with both switches (BUSINESS_RULES.md §8). */
export function useProductList(filter: ProductListFilter) {
  return useQuery({
    queryKey: cafeKeys.products(filter),
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/cafe/products", {
        params: {
          query: { CategoryId: filter.categoryId, Page: filter.page, PageSize: productsPageSize },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return toPage(data, productsPageSize);
    },
  });
}

/**
 * What the till may offer: switched on, in a category that is switched on. `isActive=true` asks
 * the API exactly that question, so a ناموجود item never reaches the grid (BUSINESS_RULES.md §8).
 */
export function useSellableProducts() {
  return useQuery({
    queryKey: cafeKeys.sellable(),
    queryFn: () =>
      fetchAll(async (page) => {
        const { data, error } = await api.GET("/api/cafe/products", {
          params: { query: { IsActive: true, Page: page, PageSize: maxPageSize } },
        });
        if (error !== undefined) {
          throw error;
        }
        return data;
      }),
  });
}

/** Any change to the price list refreshes every cafe list; orders keep their own snapshots. */
function useCafeMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: cafeKeys.all });
    },
  });
}

export function useCreateProductCategory() {
  return useCafeMutation(async (name: string) => {
    const { data, error } = await api.POST("/api/cafe/categories", { body: { name } });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useRenameProductCategory() {
  return useCafeMutation(
    async ({ id, name, version }: { id: string; name: string; version: number | string }) => {
      const { data, error } = await api.PUT("/api/cafe/categories/{id}", {
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

export function useSetProductCategoryActive() {
  return useCafeMutation(async ({ id, active }: { id: string; active: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = active
      ? await api.POST("/api/cafe/categories/{id}/activate", options)
      : await api.POST("/api/cafe/categories/{id}/deactivate", options);
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useDeleteProductCategory() {
  return useCafeMutation(async (id: string) => {
    const { error } = await api.DELETE("/api/cafe/categories/{id}", {
      params: { path: { id } },
    });
    if (error !== undefined) {
      throw error;
    }
  });
}

export interface ProductInput {
  name: string;
  categoryId: string;
  /** The plain decimal string the API reads (`normalizeMoney`), never a float. */
  price: string;
}

export function useCreateProduct() {
  return useCafeMutation(async (body: ProductInput) => {
    const { data, error } = await api.POST("/api/cafe/products", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useUpdateProduct() {
  return useCafeMutation(
    async ({ id, ...body }: ProductInput & { id: string; version: number | string }) => {
      const { data, error } = await api.PUT("/api/cafe/products/{id}", {
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

export function useSetProductActive() {
  return useCafeMutation(async ({ id, active }: { id: string; active: boolean }) => {
    const options = { params: { path: { id } } };
    const { data, error } = active
      ? await api.POST("/api/cafe/products/{id}/activate", options)
      : await api.POST("/api/cafe/products/{id}/deactivate", options);
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export interface CafeOrderListFilter {
  /** ISO business dates, inclusive. Omitted: no bound. */
  from?: string;
  to?: string;
  /** Only orders a guest left unpaid (BUSINESS_RULES.md §7 *Guest visit*): «پرداخت‌نشده — مهمان». */
  unpaidGuest?: boolean;
  page: number;
}

/** Order history for the whole counter, newest first, cancelled orders included (§8). */
export function useCafeOrderList(filter: CafeOrderListFilter, { enabled = true } = {}) {
  return useQuery({
    queryKey: cafeKeys.orders(filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/cafe/orders", {
        params: {
          query: {
            From: filter.from,
            To: filter.to,
            UnpaidGuest: filter.unpaidGuest === true ? true : undefined,
            Page: filter.page,
            PageSize: cafeOrdersPageSize,
          },
        },
      });
      if (error !== undefined) {
        throw error;
      }
      return toPage(data, cafeOrdersPageSize);
    },
  });
}

/** One member's purchases, for the tab on their profile. */
export function useMemberCafeOrders(memberId: string, page: number, { enabled = true } = {}) {
  return useQuery({
    queryKey: cafeKeys.memberOrders(memberId, page),
    enabled,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/members/{memberId}/cafe-orders", {
        params: { path: { memberId }, query: { Page: page, PageSize: cafeOrdersPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return toPage(data, cafeOrdersPageSize);
    },
  });
}

/**
 * What one visit bought, cancelled orders left out: what check-out shows the member before they
 * leave (BUSINESS_RULES.md §8). A visit buys a handful of things, so one page is all of it.
 *
 * Polled like the locker map: the till may be on another computer, and what it rings up for a
 * member who is inside joins this visit without anybody here pressing anything.
 */
export function useVisitCafeOrders(attendanceId: string) {
  return useQuery({
    queryKey: cafeKeys.visitOrders(attendanceId),
    refetchInterval: currentlyInsideRefetchMs,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/cafe/orders", {
        params: { query: { AttendanceId: attendanceId, Page: 1, PageSize: maxPageSize } },
      });
      if (error !== undefined) {
        throw error;
      }
      return data.items.filter((order) => order.cancelledAt === null);
    },
  });
}

/**
 * An order, its cancellation or a payment against it moves money, so beside the cafe lists the
 * member's debt and payment history are refreshed too: an order on account is part of the debt
 * (BUSINESS_RULES.md §5 Member debt), and a cancellation writes refunds. The "currently inside"
 * board carries each visit's orders, so it is refreshed as well.
 */
function useOrderMutation<TArgs, TResult>(request: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: request,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: cafeKeys.all }),
        queryClient.invalidateQueries({ queryKey: memberKeys.all }),
        queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
        queryClient.invalidateQueries({ queryKey: attendanceKeys.all }),
        // The map marks a holder who owes money «بدهکار», a guest included.
        queryClient.invalidateQueries({ queryKey: lockerKeys.all }),
      ]);
    },
  });
}

export interface PaymentInput {
  /** The plain decimal string the API reads. */
  amount: string;
  method: PaymentMethod;
  referenceNumber: string | null;
}

export interface CreateCafeOrderInput {
  /** Null for a walk-in customer. */
  memberId: string | null;
  items: { productId: string; quantity: number }[];
  /** Null leaves the whole order on the member's account. */
  payment: PaymentInput | null;
  /** The open visit it is bought during, from the "currently inside" board; omitted at the till. */
  attendanceId?: string;
}

export function useCreateCafeOrder() {
  return useOrderMutation(async (body: CreateCafeOrderInput) => {
    const { data, error } = await api.POST("/api/cafe/orders", { body });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

export function useCancelCafeOrder() {
  return useOrderMutation(async ({ id, reason }: { id: string; reason: string }) => {
    const { data, error } = await api.POST("/api/cafe/orders/{id}/cancel", {
      params: { path: { id } },
      body: { reason },
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}

/** Settling an order left on a member's account, in as many instalments as it takes (§5). */
export function useRegisterCafeOrderPayment() {
  return useOrderMutation(async ({ orderId, ...body }: PaymentInput & { orderId: string }) => {
    const { data, error } = await api.POST("/api/cafe/orders/{id}/payments", {
      params: { path: { id: orderId } },
      body,
    });
    if (error !== undefined) {
      throw error;
    }
    return data;
  });
}
