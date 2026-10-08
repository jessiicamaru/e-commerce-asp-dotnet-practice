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

export const useUploadProductImage = (productId: string) =>
  useListingMutation((file: File) => Product.uploadImage(productId, file), productId)

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
