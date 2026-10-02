import { useQuery } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Payment } from '@ecommerce/core/services/payment'

/** Whether payments go through the stand-in (specs/134). It changes with a deployment, not between pages. */
export function usePaymentIsStub() {
  return useQuery({ queryKey: queryKeys.paymentProvider(), queryFn: () => Payment.isStub(), staleTime: 10 * 60_000, retry: false })
}
