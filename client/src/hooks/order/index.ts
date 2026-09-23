import { useRef } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ORDER_POLL_LIMIT_MS, ORDER_POLL_MS, isSettling } from '@/constants/order'
import { queryKeys } from '@/constants/query-keys'
import { Order } from '@/services/order'
import type { CheckoutChoice } from '@/services/order/types'

export function useShippingOptions() {
  return useQuery({
    queryKey: queryKeys.shippingOptions(),
    queryFn: () => Order.shippingOptions(),
    staleTime: 5 * 60_000,
  })
}

/**
 * What checkout would charge for these choices. Order prices it with the same code that prices the
 * order itself (#38), so this is the amount charged unless the cart or a price changes in between.
 */
export function useCheckoutQuote(choice: CheckoutChoice | null) {
  return useQuery({
    queryKey: queryKeys.checkoutQuote(choice ?? { addressId: null, shippingOption: '' }),
    queryFn: () => Order.quote(choice!),
    enabled: choice !== null && choice.shippingOption !== '',
    // An empty cart or an unsellable line will not fix itself on a retry.
    retry: false,
  })
}

export function usePlaceOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (choice: CheckoutChoice) => Order.place(choice),
    onSuccess: () => {
      // The cart is emptied by Cart when the order completes, and the list gains a row either way.
      void queryClient.invalidateQueries({ queryKey: queryKeys.cart() })
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
    },
  })
}

/**
 * One order, asked about again every second while the saga is still settling it, and no longer than
 * {@link ORDER_POLL_LIMIT_MS}. Push would be better; polling is what #38 chose to start with.
 */
export function useOrder(id: string) {
  // Set on the first poll decision rather than during render: reading the clock while rendering makes
  // the result depend on when React happened to re-render.
  const startedAt = useRef<number | null>(null)

  return useQuery({
    queryKey: queryKeys.order(id),
    queryFn: () => Order.get(id),
    enabled: id !== '',
    retry: false,
    refetchInterval: (query) => {
      const order = query.state.data
      if (!order || !isSettling(order.status)) {
        return false
      }

      startedAt.current ??= Date.now()
      return Date.now() - startedAt.current < ORDER_POLL_LIMIT_MS ? ORDER_POLL_MS : false
    },
  })
}

export function useMyOrders(page: number, pageSize: number) {
  return useQuery({
    queryKey: queryKeys.myOrders(page),
    queryFn: () => Order.listMine(page, pageSize),
    placeholderData: (previous) => previous,
  })
}

/**
 * The seller's sales. `enabled` keeps a customer who is not a seller from asking and collecting a
 * 403 - which is the server's decision either way; this only avoids the pointless request.
 */
export function useMySales(page: number, pageSize: number, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.mySales(page),
    queryFn: () => Order.sales(page, pageSize),
    enabled,
    placeholderData: (previous) => previous,
  })
}

/** One sale. A 404 will not turn into something else on a retry. */
export function useSale(id: string) {
  return useQuery({
    queryKey: queryKeys.sale(id),
    queryFn: () => Order.sale(id),
    enabled: id !== '',
    retry: false,
  })
}

/**
 * A seller's two steps on their part. Both re-read the sale and the sales list rather than patching
 * them: the server decides what the part and the address now are (the address goes once it is shipped).
 */
export function useMoveSale(id: string) {
  const queryClient = useQueryClient()
  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.sale(id) })
    await queryClient.invalidateQueries({ queryKey: ['sales'] })
  }

  return {
    prepare: useMutation({ mutationFn: () => Order.prepareSale(id), onSuccess: refresh }),
    ship: useMutation({ mutationFn: (trackingReference: string) => Order.shipSale(id, trackingReference), onSuccess: refresh }),
  }
}
