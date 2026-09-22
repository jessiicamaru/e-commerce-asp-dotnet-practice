import type { TFunction } from 'i18next'

/**
 * Why a cart line cannot be bought - as a translation key, not a sentence (specs/021). Null when it
 * can be bought, which is the common case and says nothing.
 */
export function lineProblemKey(status: string): string | null {
  switch (status) {
    case 'Available':
      return null
    case 'NotForSale':
      return 'status.notForSale'
    case 'NoLongerAvailable':
      return 'status.noLongerAvailable'
    case 'PriceUnavailable':
      return 'status.priceUnavailable'
    default:
      return 'status.unavailable'
  }
}

export function lineProblem(t: TFunction<'cart'>, status: string): string | null {
  const key = lineProblemKey(status)
  return key ? t(key) : null
}
