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
