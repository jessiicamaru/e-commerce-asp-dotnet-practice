import { useSyncExternalStore } from 'react'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { Cart } from '@ecommerce/core/services/cart'
import type { Cart as CartModel } from '@ecommerce/core/services/cart/types'
import { Product } from '@ecommerce/core/services/product'
import {
  addToGuestCart,
  readGuestCart,
  removeFromGuestCart,
  setGuestCartQuantity,
  subscribeToGuestCart,
  type GuestCartLine,
} from '@ecommerce/core/utils/cart/guest-cart'

/**
 * The cart a page shows (specs/162): the account's when signed in, otherwise this browser's - read through the same
 * server pricing, so both look alike. Nothing is asked while the session is still being restored.
 */
export function useCart(): { data: CartModel | undefined; isPending: boolean; isError: boolean; isGuest: boolean } {
  const { user, restoring } = useAuth()
  const guest = useGuestCartLines()

  const account = useQuery({ queryKey: queryKeys.cart(), queryFn: () => Cart.get(), enabled: !restoring && !!user })
  const browser = useQuery({
    queryKey: [...queryKeys.cart(), 'guest', guest] as const,
    queryFn: () => Cart.price(guest),
    enabled: !restoring && !user && guest.length > 0,
    placeholderData: (previous) => previous,
  })

  if (restoring) return { data: undefined, isPending: true, isError: false, isGuest: false }
  if (user) return { data: account.data, isPending: account.isPending, isError: account.isError, isGuest: false }
  // An empty browser cart needs no request: it is empty.
  if (guest.length === 0) return { data: EMPTY, isPending: false, isError: false, isGuest: true }
  return { data: browser.data, isPending: browser.isPending, isError: browser.isError, isGuest: true }
}

const EMPTY: CartModel = { lines: [], estimatedTotal: 0, canCheckOut: false, pricesAvailable: true, currency: '' }

/** This browser's lines, kept in step with every change - in this tab and in others. */
export function useGuestCartLines(): GuestCartLine[] {
  return useSyncExternalStore(subscribeToGuestCart, readGuestCart, readGuestCart)
}

/** How many units the header shows: the account's cart, or this browser's when signed out. */
export function useCartCount(): number {
  const { user } = useAuth()
  const guest = useGuestCartLines()
  const account = useQuery({ queryKey: queryKeys.cart(), queryFn: () => Cart.get(), enabled: !!user })
  return user ? (account.data?.lines.length ?? 0) : guest.length
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

/** A change to this browser's cart; refused (and so an error) only when storage would not keep it. */
async function inBrowser(kept: boolean): Promise<void> {
  if (!kept) throw new GuestCartUnavailable()
}

/** This browser cannot keep a cart (storage blocked or full, or 50 lines): the shopper signs in instead. */
export class GuestCartUnavailable extends Error {
  constructor() {
    super('This browser cannot keep a cart.')
    this.name = 'GuestCartUnavailable'
  }
}

/** Adds to the account's cart, or to this browser's when signed out (specs/162). */
export const useAddToCart = () => {
  const { user } = useAuth()
  return useCartMutation((productId: string, quantity: number, variantId?: string) =>
    user
      ? Cart.addItem(productId, quantity, variantId)
      : inBrowser(addToGuestCart(productId, variantId ?? productId, quantity)),
  )
}

export const useSetCartQuantity = () => {
  const { user } = useAuth()
  return useCartMutation((variantId: string, quantity: number) =>
    user ? Cart.setQuantity(variantId, quantity) : inBrowser(setGuestCartQuantity(variantId, quantity)),
  )
}

export const useRemoveCartLine = () => {
  const { user } = useAuth()
  return useCartMutation((variantId: string) =>
    user ? Cart.removeItem(variantId) : inBrowser(removeFromGuestCart(variantId)),
  )
}

export const useEmptyCart = () => useCartMutation(() => Cart.empty())
