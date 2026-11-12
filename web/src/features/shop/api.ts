import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/**
 * The ways a customer can reach the shop (BUSINESS_RULES.md §16 *The shop admin*): one set for the
 * whole shop. Each is `null` until the shop admin gives it.
 */
export type ShopContacts = components["schemas"]["ShopContactsResponse"];

export type ShopContactsInput = components["schemas"]["UpdateShopContactsCommand"];

/** A category with its number of products (§16 *Products*: one level, deleted only while empty). */
export type ShopCategory = components["schemas"]["ShopCategoryResponse"];

/** One card of the product list. */
export type ShopProductListItem = components["schemas"]["ShopProductListItem"];

/** A whole product, as its page shows it. */
export type ShopProduct = components["schemas"]["ShopProductResponse"];

export type ShopVariant = components["schemas"]["ShopVariantResponse"];

export type ShopPhoto = components["schemas"]["ShopPhotoResponse"];

export type ShopProductInput = components["schemas"]["CreateShopProductCommand"];

export const shopKeys = {
  all: ["shop"] as const,
  contacts: () => [...shopKeys.all, "contacts"] as const,
  categories: () => [...shopKeys.all, "categories"] as const,
  products: () => [...shopKeys.all, "products"] as const,
  product: (id: string) => [...shopKeys.all, "products", id] as const,
};

/**
 * A product photo's address: public, cached for good by the browser (an id never gets other
 * bytes). `small` (480 px) for lists, `large` (1600 px) for the product's page.
 */
export function photoUrl(photoId: string, size: "small" | "large"): string {
  return `/api/shop/photos/${photoId}/${size}`;
}

function unwrap<T>({ data, error }: { data?: T; error?: unknown }): T {
  if (error !== undefined) {
    throw error;
  }
  return data as T;
}

/** The shop admin edits them; the Owner reads them (§16). */
export function useShopContacts() {
  return useQuery({
    queryKey: shopKeys.contacts(),
    queryFn: async () => unwrap(await api.GET("/api/shop/contacts")),
  });
}

/** The shop admin only; the API refuses the Owner. */
export function useUpdateShopContacts() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: ShopContactsInput) =>
      unwrap(await api.PUT("/api/shop/contacts", { body })),
    onSuccess: (contacts) => {
      queryClient.setQueryData(shopKeys.contacts(), contacts);
    },
  });
}

export function useShopCategories() {
  return useQuery({
    queryKey: shopKeys.categories(),
    queryFn: async () => unwrap(await api.GET("/api/shop/categories")),
  });
}

/** Every change to a category also changes the product list's category names. */
function useInvalidateCatalog() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: shopKeys.all });
}

export function useCreateShopCategory() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (name: string) =>
      unwrap(await api.POST("/api/shop/categories", { body: { name } })),
    onSuccess: invalidate,
  });
}

export function useRenameShopCategory() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async ({
      id,
      name,
      version,
    }: {
      id: string;
      name: string;
      version: number | string;
    }) =>
      unwrap(
        await api.PUT("/api/shop/categories/{id}", {
          params: { path: { id } },
          body: { name, version },
        }),
      ),
    onSuccess: invalidate,
  });
}

export function useDeleteShopCategory() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (id: string) => {
      const { error } = await api.DELETE("/api/shop/categories/{id}", { params: { path: { id } } });
      if (error !== undefined) {
        throw error;
      }
    },
    onSuccess: invalidate,
  });
}

export function useShopProducts() {
  return useQuery({
    queryKey: shopKeys.products(),
    queryFn: async () => unwrap(await api.GET("/api/shop/products")),
  });
}

export function useShopProduct(id: string) {
  return useQuery({
    queryKey: shopKeys.product(id),
    queryFn: async () =>
      unwrap(await api.GET("/api/shop/products/{id}", { params: { path: { id } } })),
  });
}

/** After any change to a product: its page gets the answer, the list reloads. */
function useProductSaved() {
  const queryClient = useQueryClient();
  return (product: ShopProduct) => {
    queryClient.setQueryData(shopKeys.product(product.id), product);
    void queryClient.invalidateQueries({ queryKey: shopKeys.products(), exact: true });
    void queryClient.invalidateQueries({ queryKey: shopKeys.categories() });
  };
}

export function useCreateShopProduct() {
  const saved = useProductSaved();
  return useMutation({
    mutationFn: async (body: ShopProductInput) =>
      unwrap(await api.POST("/api/shop/products", { body })),
    onSuccess: saved,
  });
}

export function useUpdateShopProduct(id: string) {
  const saved = useProductSaved();
  return useMutation({
    mutationFn: async (body: ShopProductInput & { version: number | string }) =>
      unwrap(await api.PUT("/api/shop/products/{id}", { params: { path: { id } }, body })),
    onSuccess: saved,
  });
}

/** One photo, as `multipart/form-data` with the field `file`. */
export function useAddShopPhoto(productId: string) {
  const saved = useProductSaved();
  return useMutation({
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return unwrap(
        await api.POST("/api/shop/products/{id}/photos", {
          params: { path: { id: productId } },
          // The form goes as it is; the browser writes its multipart boundary.
          body: form as never,
          bodySerializer: (body: unknown) => body as FormData,
        }),
      );
    },
    onSuccess: saved,
  });
}

export function useRemoveShopPhoto(productId: string) {
  const saved = useProductSaved();
  return useMutation({
    mutationFn: async (photoId: string) =>
      unwrap(
        await api.DELETE("/api/shop/products/{id}/photos/{photoId}", {
          params: { path: { id: productId, photoId } },
        }),
      ),
    onSuccess: saved,
  });
}

export function useReorderShopPhotos(productId: string) {
  const saved = useProductSaved();
  return useMutation({
    mutationFn: async (photoIds: string[]) =>
      unwrap(
        await api.PUT("/api/shop/products/{id}/photos/order", {
          params: { path: { id: productId } },
          body: { photoIds },
        }),
      ),
    onSuccess: saved,
  });
}
