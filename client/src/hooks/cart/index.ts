import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@/constants/query-keys'
import { Cart } from '@/services/cart'

export function useCart(enabled = true) {
  return useQuery({ queryKey: queryKeys.cart(), queryFn: () => Cart.get(), enabled })
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
