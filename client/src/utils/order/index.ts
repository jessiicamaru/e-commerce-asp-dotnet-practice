import { ORDER_STATUS } from '@/constants/order'

/**
 * An order's status as a sentence a customer understands (#39).
 *
 * The saga's failure reasons are written for operators ("Insufficient stock for product 01a0..."), so
 * they are classified here rather than shown raw.
 */
export function describeOrderStatus(status: string, failureReason: string | null): string {
  switch (status) {
    case ORDER_STATUS.submitted:
      return 'We are reserving your items and taking payment…'
    case ORDER_STATUS.paid:
      return 'Paid. We will start preparing it soon.'
    case ORDER_STATUS.preparing:
      return 'Being prepared for dispatch.'
    case ORDER_STATUS.shipped:
      return 'On its way.'
    case ORDER_STATUS.failed:
      if (failureReason && /stock/i.test(failureReason)) {
        return 'Not placed: some items ran out of stock. Nothing was charged, and your cart is unchanged.'
      }
      if (failureReason && /payment|declin/i.test(failureReason)) {
        return 'Not placed: the payment was declined. Your cart is unchanged, so you can try again.'
      }
      return 'Not placed. Nothing was charged, and your cart is unchanged.'
    default:
      return status
  }
}

/** Which tone to show a status in. */
export function orderStatusTone(status: string): 'good' | 'bad' | 'waiting' {
  if (status === ORDER_STATUS.failed) return 'bad'
  if (status === ORDER_STATUS.submitted) return 'waiting'
  return 'good'
}
