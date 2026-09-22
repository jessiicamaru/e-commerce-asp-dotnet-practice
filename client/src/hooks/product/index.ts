import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { CURRENCIES } from '@/config/money'
import { Product } from '@/services/product'
import type { NewProduct, ProductQuery } from '@/services/product/types'

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
      queryFn: () => Product.get(id, currency),
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
