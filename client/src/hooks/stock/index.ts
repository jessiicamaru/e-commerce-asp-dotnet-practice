import { useMutation, useQueries, useQueryClient } from '@tanstack/react-query'
import { Stock } from '@/services/stock'

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
