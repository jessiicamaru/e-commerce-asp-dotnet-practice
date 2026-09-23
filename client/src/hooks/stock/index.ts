import { useMutation, useQueries, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Product } from '@/services/product'
import { Stock } from '@/services/stock'
import { sumStock } from '@/utils/stock'

/**
 * The stock of every variant of one product, for the seller's page.
 *
 * `useQueries` rather than one call: Inventory keys stock by variant, and a product can have
 * several. A variant whose stock row has not arrived from the broker yet answers 404 — that is a
 * real state on a freshly listed product (specs/031), so it is surfaced rather than retried away.
 */
export function useVariantStock(variantIds: string[]) {
  return useQueries({
    queries: variantIds.map((id) => ({
      queryKey: ['stock', id] as const,
      queryFn: () => Stock.get(id),
      retry: false,
    })),
    combine: (results) => ({
      isPending: results.some((r) => r.isPending),
      byVariant: Object.fromEntries(
        variantIds.map((id, index) => [id, results[index].data ?? null]),
      ),
    }),
  })
}

/**
 * The stock of several listings at once, for the seller's product table.
 *
 * A listing in a page carries no variants (`variants: null`), and Inventory keys stock by variant, so
 * this reads each product once - the same cached read its own page uses - and then each of its
 * variants' stock. It is two waves of small requests for one page of one seller's listings, which is
 * what the page size bounds.
 */
export function useListingStock(productIds: string[]) {
  const details = useQueries({
    queries: productIds.map((id) => ({
      queryKey: queryKeys.product(id),
      queryFn: () => Product.get(id),
      retry: false,
    })),
  })

  const variantsOf = Object.fromEntries(
    productIds.map((id, index) => [id, (details[index].data?.variants ?? []).map((variant) => variant.id)]),
  )
  const allVariants = Object.values(variantsOf).flat()

  const stock = useVariantStock(allVariants)

  return {
    isPending: details.some((detail) => detail.isPending) || stock.isPending,
    byProduct: Object.fromEntries(
      productIds.map((id) => [id, sumStock(variantsOf[id].map((variantId) => stock.byVariant[variantId]))]),
    ),
  }
}

export function useSetStock(productId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ variantId, quantityOnHand }: { variantId: string; quantityOnHand: number }) =>
      Stock.setOnHand(variantId, quantityOnHand),
    onSuccess: async (_data, { variantId }) => {
      await queryClient.invalidateQueries({ queryKey: ['stock', variantId] })
      // Catalog's availability is a read model fed by a message, so it is SECONDS behind on
      // purpose. Re-reading is how this page catches up; patching the cache would show "InStock"
      // before the catalogue agrees.
      await queryClient.invalidateQueries({ queryKey: ['product', productId] })
      await queryClient.invalidateQueries({ queryKey: ['my-products'] })
      await queryClient.invalidateQueries({ queryKey: ['products'] })
    },
  })
}
