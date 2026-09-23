import type { TFunction } from 'i18next'

/**
 * A sale's status in the seller's words. Only three can arrive - Order never reports a failed or
 * still-settling order as a sale - and anything else is shown as sent rather than guessed at.
 */
export function describeSaleStatus(t: TFunction<'seller'>, status: string): string {
  switch (status) {
    case 'Paid':
      return t('sales.status.paid')
    case 'Preparing':
      return t('sales.status.preparing')
    case 'Shipped':
      return t('sales.status.shipped')
    case 'Cancelled':
      return t('sales.status.cancelled')
    default:
      return status
  }
}
