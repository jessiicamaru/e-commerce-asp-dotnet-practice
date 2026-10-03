import { useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { PAYMENT_CHECKOUT_POLL_MS } from '@ecommerce/core/constants/payment'
import { Payment } from '@ecommerce/core/services/payment'

/** What Payment says about itself (specs/134, specs/143). It changes with a deployment, not between pages. */
export function usePaymentAbout() {
  return useQuery({ queryKey: queryKeys.paymentProvider(), queryFn: () => Payment.about(), staleTime: 10 * 60_000, retry: false })
}

/**
 * An order's payment while the order waits (specs/143): asked again every few seconds while Payment is preparing it or
 * the customer has yet to pay, and not at all once the order has settled.
 */
export function usePaymentCheckout(orderId: string, enabled: boolean) {
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: queryKeys.paymentCheckout(orderId),
    queryFn: () => Payment.checkout(orderId),
    enabled: enabled && orderId !== '',
    retry: false,
    refetchInterval: (query) => {
      const state = query.state.data?.state
      return state === undefined || state === 'Preparing' || state === 'AwaitingPayment' ? PAYMENT_CHECKOUT_POLL_MS : false
    },
  })

  // Decided at the gateway: the order settles a moment later, so it is read again rather than left to a poll that may
  // have given up while the customer was paying.
  const state = query.data?.state
  useEffect(() => {
    if (state === 'Paid' || state === 'Failed') {
      void queryClient.invalidateQueries({ queryKey: queryKeys.order(orderId) })
    }
  }, [state, orderId, queryClient])

  return query
}
