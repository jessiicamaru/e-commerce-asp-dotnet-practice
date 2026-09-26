import type { TFunction } from 'i18next'
import type { VoucherAmount, VoucherSummary } from '@/services/voucher/types'
import { money } from '@/utils/shared'

/**
 * A voucher in words (specs/070): what it gives in each currency it has an amount for - "30% off · up to
 * ₫200,000 · on orders from ₫1,000,000" - and what else must hold. One line per currency, because a voucher's
 * money is never converted (specs/022) and each currency's cap and minimum are their own.
 */
export function describeBenefit(t: TFunction<'vouchers'>, voucher: Pick<VoucherSummary, 'benefit' | 'percent' | 'amounts'>): string[] {
  return voucher.amounts.map((amount) => {
    const parts = [head(t, voucher, amount)]
    if (amount.maxDiscount !== null && voucher.benefit !== 'FixedAmount') {
      parts.push(t('describe.cap', { amount: money(amount.maxDiscount, amount.currency) }))
    }
    if (amount.minSubtotal !== null && amount.minSubtotal > 0) {
      parts.push(t('describe.minimum', { amount: money(amount.minSubtotal, amount.currency) }))
    }
    return parts.join(' · ')
  })
}

function head(t: TFunction<'vouchers'>, voucher: Pick<VoucherSummary, 'benefit' | 'percent'>, amount: VoucherAmount): string {
  switch (voucher.benefit) {
    case 'Percent':
      return t('describe.percent', { percent: voucher.percent })
    case 'FixedAmount':
      return t('describe.fixed', { amount: money(amount.fixedValue ?? 0, amount.currency) })
    case 'FreeShipping':
      return t('describe.freeShipping')
    default:
      return voucher.benefit
  }
}

/** Who and what it is for: its conditions, then whether it applies to everything or to some products. */
export function describeRules(t: TFunction<'vouchers'>, voucher: Pick<VoucherSummary, 'conditions' | 'targets'>): string[] {
  const rules = voucher.conditions.map((c) =>
    c.type === 'NewCustomer'
      ? t('describe.newCustomer')
      : c.type === 'FirstOrderInShop'
        ? t('describe.firstInShop')
        : c.type === 'MinQuantity'
          ? t('describe.minQuantity', { count: c.value ?? 0 })
          : c.type,
  )

  rules.push(voucher.targets.length > 0 ? t('describe.products', { count: voucher.targets.length }) : t('describe.everything'))
  return rules
}

/** How far through its uses it is. */
export function describeUses(t: TFunction<'vouchers'>, voucher: Pick<VoucherSummary, 'usedCount' | 'totalLimit' | 'perCustomerLimit'>): string {
  const uses = voucher.totalLimit === null
    ? t('describe.used', { count: voucher.usedCount })
    : t('describe.usedOf', { count: voucher.usedCount, limit: voucher.totalLimit })
  return voucher.perCustomerLimit === null ? uses : `${uses} · ${t('describe.perCustomer', { count: voucher.perCustomerLimit })}`
}
