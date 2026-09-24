import type { TFunction } from 'i18next'
import type { AppNotification } from '@/services/notifications/types'
import { money } from '@/utils/shared'

/**
 * A notification in the reader's words (specs/042 research D1). The server stores what happened - a kind
 * and its data - never a sentence, so the same notice reads in Vietnamese today and English tomorrow.
 *
 * A kind this storefront has no words for yet is still shown, generically, rather than hidden: a newer
 * service may send one before the storefront learns it. So is a notice missing a value its sentence needs
 * (specs/048): a vaguer sentence beats one with a hole in it. The keys each kind carries are declared in
 * `Ecommerce.Shared/Notifications/notification-kinds.json`, which the tests hold this against.
 */
export function describeNotification(t: TFunction<'notifications'>, n: AppNotification): string {
  const d = n.data
  const rating = Number(d.rating)
  const values: Record<string, string> = {
    order: d.orderId ? d.orderId.slice(0, 8) : '',
    total: d.total && d.currency ? money(Number(d.total), d.currency) : '',
    amount: d.amount && d.currency ? money(Number(d.amount), d.currency) : '',
    tracking: d.tracking ?? '',
    // A parcel with no seller is the shop's own; a shop application with no name is a hole.
    shop: d.shop ?? (n.kind === 'ParcelShipped' ? t('theShop') : ''),
    by: d.by === 'Customer' ? t('byYou') : t('byShop'),
    product: d.product ?? '',
    reason: d.reason ?? '',
    rating: d.rating ?? '',
  }
  const key = `kind.${n.kind}`
  const options = { defaultValue: '', ...(d.rating && Number.isFinite(rating) ? { count: rating } : {}) }

  const raw = t(key, { ...options, skipInterpolation: true })
  const holes = [...raw.matchAll(/{{\s*(\w+)\s*}}/g)].map((m) => m[1]).filter((name) => name !== 'count' && !values[name])
  if (!raw || holes.length > 0) return t('generic')

  return t(key, { ...options, ...values })
}
