import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Cart } from '@/services/cart'
import { Product } from '@/services/product'

export function useCart(enabled = true) {
  return useQuery({ queryKey: queryKeys.cart(), queryFn: () => Cart.get(), enabled })
}

/**
 * The picture for each cart line, keyed by variant id.
 *
 * <p>
 * The cart does not carry pictures - Cart asks Catalog for names and prices, not images - and adding
 * one would change the contract between three services for something only this page shows. So the
 * page reads each product once, through the same cached request the product page uses, and takes the
 * chosen <b>variant's</b> picture, which the server has already folded the product's into when the
 * variant has none (specs/032).
 * </p>
 * <p>
 * A line whose product has gone (deleted since it was added) simply has no picture; the line itself
 * already says why it cannot be bought.
 * </p>
 */
export function useCartImages(lines: { productId: string; variantId: string }[]) {
  const productIds = [...new Set(lines.map((line) => line.productId))]

  return useQueries({
    queries: productIds.map((id) => ({
      queryKey: queryKeys.product(id),
      queryFn: () => Product.get(id),
      retry: false,
    })),
    combine: (results) => {
      const byVariant: Record<string, string | null> = {}
      results.forEach((result) => {
        for (const variant of result.data?.variants ?? []) {
          byVariant[variant.id] = variant.imageUrl
        }
      })
      return byVariant
    },
  })
}

/**
 * Every change re-reads the cart rather than editing a local copy: the names and prices on it come
 * from Catalog at read time, so only the server can say what the cart now looks like.
 */
function useCartMutation<TArgs extends unknown[]>(action: (...args: TArgs) => Promise<void>) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (args: TArgs) => action(...args),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.cart() }),
  })
}

export const useAddToCart = () =>
  useCartMutation((productId: string, quantity: number, variantId?: string) =>
    Cart.addItem(productId, quantity, variantId),
  )

export const useSetCartQuantity = () =>
  useCartMutation((productId: string, quantity: number) => Cart.setQuantity(productId, quantity))

export const useRemoveCartLine = () => useCartMutation((productId: string) => Cart.removeItem(productId))

export const useEmptyCart = () => useCartMutation(() => Cart.empty())
