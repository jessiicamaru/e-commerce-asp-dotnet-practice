import type { TFunction } from 'i18next'
import { ORDER_STATUS } from '@/constants/order'

/**
 * Which sentence describes an order's status (specs/021: the key, not the sentence).
 *
 * The saga's failure reasons are written for operators ("Insufficient stock for product 01a0..."), so
 * they are classified here into something a customer reads - and the classification decides a
 * translation key rather than an English sentence, because the same order is read in two languages.
 */
export function orderStatusKey(status: string, failureReason: string | null): string {
  switch (status) {
    case ORDER_STATUS.submitted:
      return 'status.submitted'
    case ORDER_STATUS.paid:
      return 'status.paid'
    case ORDER_STATUS.preparing:
      return 'status.preparing'
    case ORDER_STATUS.shipped:
      return 'status.shipped'
    case ORDER_STATUS.cancelled:
      return 'status.cancelled'
    case ORDER_STATUS.failed:
      if (failureReason && /stock/i.test(failureReason)) {
        return 'status.failedStock'
      }
      if (failureReason && /payment|declin/i.test(failureReason)) {
        return 'status.failedPayment'
      }
      return 'status.failed'
    default:
      return ''
  }
}

/** The sentence itself. A status nobody has a sentence for shows as it came, rather than as a key. */
export function describeOrderStatus(
  t: TFunction<'orders'>,
  status: string,
  failureReason: string | null,
): string {
  const key = orderStatusKey(status, failureReason)
  return key ? t(key) : status
}

/** Which tone to show a status in. */
export function orderStatusTone(status: string): 'good' | 'bad' | 'waiting' {
  if (status === ORDER_STATUS.failed || status === ORDER_STATUS.cancelled) return 'bad'
  if (status === ORDER_STATUS.submitted) return 'waiting'
  return 'good'
}
