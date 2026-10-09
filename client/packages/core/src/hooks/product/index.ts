import { useSyncExternalStore } from 'react'
import { readRecentlyViewed, subscribeToRecentlyViewed } from '@ecommerce/core/utils/product/recently-viewed'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { CURRENCIES } from '@ecommerce/core/config/money'
import { Product } from '@ecommerce/core/services/product'
import type {
  NewProduct,
  NewVariant,
  ProductDetails,
  ProductQuery,
  ProductSpecificationValue,
  ProductText,
} from '@ecommerce/core/services/product/types'

/** Others like this one (specs/163). */
export function useRelatedProducts(productId: string) {
  return useQuery({
    queryKey: [...queryKeys.product(productId), 'related'] as const,
    queryFn: () => Product.related(productId),
    enabled: productId !== '',
  })
}

/**
 * The products this browser opened, most recent first (specs/163) - read through the listing's `ids`, which keeps only
 * what is still on the shelf, then put back in the order remembered. `exclude` leaves out the page being read.
 */
export function useRecentlyViewed(exclude?: string) {
  const ids = useSyncExternalStore(subscribeToRecentlyViewed, readRecentlyViewed, readRecentlyViewed).filter(
    (id) => id !== exclude,
  )
  const query = useQuery({
    queryKey: queryKeys.products({ ids, pageSize: ids.length }),
    queryFn: () => Product.list({ ids, pageSize: ids.length }),
    enabled: ids.length > 0,
  })
  const byId = new Map((query.data?.items ?? []).map((product) => [product.id, product]))
  return ids.map((id) => byId.get(id)).filter((product): product is NonNullable<typeof product> => !!product)
}

export function useProducts(query: ProductQuery) {
  return useQuery({
    queryKey: queryKeys.products(query),
    queryFn: () => Product.list(query),
    // The previous page stays on screen while the next one loads, instead of flashing empty.
    placeholderData: (previous) => previous,
  })
}

/** A seller's own listings. `enabled` keeps a customer from ever asking and collecting a 403. */
export function useMyProducts(query: ProductQuery, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.myProducts(query),
    queryFn: () => Product.mine(query),
    enabled,
    placeholderData: (previous) => previous,
  })
}

export function useProduct(id: string) {
  return useQuery({
    queryKey: queryKeys.product(id),
    queryFn: () => Product.get(id),
    enabled: id !== '',
    // A product that does not exist will not start existing on a retry.
    retry: false,
  })
}

/** Listing a product. Both the seller's page and the public catalogue are now out of date. */
export function useCreateProduct() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (input: NewProduct) => Product.create(input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['my-products'] })
      await queryClient.invalidateQueries({ queryKey: ['products'] })
    },
  })
}

/**
 * The writes on a seller's own listing.
 *
 * All three invalidate the same three things, because all three change what a shopper sees: the
 * listing itself, the seller's page, and the public catalogue.
 */
function useListingMutation<TVariables>(action: (variables: TVariables) => Promise<unknown>, productId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: action,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.product(productId) })
      await queryClient.invalidateQueries({ queryKey: ['my-products'] })
      await queryClient.invalidateQueries({ queryKey: ['products'] })
    },
  })
}

export const useSetVariantPrice = (productId: string) =>
  useListingMutation(
    ({ variantId, currency, amount }: { variantId: string; currency: string; amount: number }) =>
      Product.setPrice(productId, variantId, currency, amount),
    productId,
  )

/** Sets a compare-at price, or clears it when `amount` is null (specs/161). */
export const useSetCompareAtPrice = (productId: string) =>
  useListingMutation(
    ({ variantId, currency, amount }: { variantId: string; currency: string; amount: number | null }) =>
      amount === null
        ? Product.clearCompareAt(productId, variantId, currency)
        : Product.setCompareAt(productId, variantId, currency, amount),
    productId,
  )

export const useUploadProductImage = (productId: string) =>
  useListingMutation((file: File) => Product.uploadImage(productId, file), productId)

/** The gallery (specs/160): add, remove, make cover, reorder - each a listing change, so the product is read again. */
export const useAddProductPhoto = (productId: string) =>
  useListingMutation((file: File) => Product.addPhoto(productId, file), productId)

export const useRemoveProductPhoto = (productId: string) =>
  useListingMutation((photoId: string) => Product.removePhoto(productId, photoId), productId)

export const useMakeProductCover = (productId: string) =>
  useListingMutation((photoId: string) => Product.makeCover(productId, photoId), productId)

export const useReorderProductPhotos = (productId: string) =>
  useListingMutation((photoIds: string[]) => Product.reorderPhotos(productId, photoIds), productId)

export const useDeleteProduct = (productId: string) =>
  useListingMutation(() => Product.remove(productId), productId)

/** The original text and the category (specs/124). */
export const useUpdateProductDetails = (productId: string) =>
  useListingMutation((details: ProductDetails) => Product.updateDetails(productId, details), productId)

/** One language's own text: saved, or taken away when `text` is null (specs/124). */
export const useSetProductTranslation = (productId: string) =>
  useListingMutation(
    ({ language, text }: { language: string; text: ProductText | null }) =>
      text ? Product.setTranslation(productId, language, text) : Product.removeTranslation(productId, language),
    productId,
  )

export const useAddVariant = (productId: string) =>
  useListingMutation((variant: NewVariant) => Product.addVariant(productId, variant), productId)

/**
 * The same product read once per currency, for the seller's price editor.
 *
 * A response carries ONE currency's prices (specs/022) - which is right for a shopper and wrong for
 * the person setting them, who needs to see the price they are not browsing in. Asking twice is the
 * honest way to see both; the alternative would be an endpoint that returns every currency at once,
 * which is a backend change this page does not need.
 */
export function useProductInEveryCurrency(id: string) {
  return useQueries({
    queries: CURRENCIES.map((currency) => ({
      queryKey: ['product', id, currency] as const,
      // Signed in: its own seller reads a product off the shelf too (specs/124).
      queryFn: () => Product.getAsOwner(id, currency),
      enabled: id !== '',
      retry: false,
    })),
    combine: (results) => ({
      isPending: results.some((r) => r.isPending),
      isError: results.some((r) => r.isError),
      /** `{ VND: 41000000, USD: null }` per variant id - null meaning NOT SOLD, never zero. */
      byCurrency: Object.fromEntries(
        CURRENCIES.map((currency, index) => [
          currency,
          Object.fromEntries(
            (results[index].data?.variants ?? []).map((variant) => [variant.id, variant.price]),
          ),
        ]),
      ) as Record<string, Record<string, number | null>>,
      /** The same per currency, for what each price is compared against (specs/161) - null meaning none. */
      compareAtByCurrency: Object.fromEntries(
        CURRENCIES.map((currency, index) => [
          currency,
          Object.fromEntries(
            (results[index].data?.variants ?? []).map((variant) => [variant.id, variant.compareAtPrice ?? null]),
          ),
        ]),
      ) as Record<string, Record<string, number | null>>,
      product: results[0].data,
    }),
  })
}

export const useUploadVariantImage = (productId: string) =>
  useListingMutation(
    ({ variantId, file }: { variantId: string; file: File }) =>
      Product.uploadVariantImage(productId, variantId, file),
    productId,
  )

export const useRemoveVariantImage = (productId: string) =>
  useListingMutation(
    (variantId: string) => Product.removeVariantImage(productId, variantId),
    productId,
  )

/** A seller sends a rejected product back for review (specs/045); every read of it is refreshed. */
export function useResubmitProduct(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => Product.resubmit(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['product', id] }),
  })
}

/** The product's specifications, the whole set (specs/159); every read of the product is refreshed after. */
export const useSetProductSpecifications = (productId: string) =>
  useListingMutation((values: ProductSpecificationValue[]) => Product.setSpecifications(productId, values), productId)
