import type { TFunction } from 'i18next'
import type { AppNotification } from '@/services/notifications/types'
import { money } from '@/utils/shared'

/**
 * A notification in the reader's words (specs/042 research D1). The server stores what happened - a kind
 * and its data - never a sentence, so the same notice reads in Vietnamese today and English tomorrow.
 *
 * A kind this storefront has no words for yet is still shown, generically, rather than hidden: a newer
 * service may send one before the storefront learns it.
 */
export function describeNotification(t: TFunction<'notifications'>, n: AppNotification): string {
  const d = n.data
  const order = d.orderId ? d.orderId.slice(0, 8) : ''
  const known = t(`kind.${n.kind}`, {
    defaultValue: '',
    order,
    total: d.total && d.currency ? money(Number(d.total), d.currency) : '',
    amount: d.amount && d.currency ? money(Number(d.amount), d.currency) : '',
    tracking: d.tracking ?? '',
    shop: d.shop ?? t('theShop'),
    by: d.by === 'Customer' ? t('byYou') : t('byShop'),
  })
  return known || t('generic')
}
