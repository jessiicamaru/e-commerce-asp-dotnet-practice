import type { Benefit, NewVoucher, VoucherCondition } from '@/services/voucher/types'

export interface AmountRow {
  currency: string
  fixedValue: string
  maxDiscount: string
  minSubtotal: string
}

const number = (text: string): number | null => (text.trim() === '' ? null : Number(text))
const iso = (local: string): string | null => (local ? new Date(local).toISOString() : null)
export const emptyRow = (currency: string): AmountRow => ({ currency, fixedValue: '', maxDiscount: '', minSubtotal: '' })

/**
 * What creating a voucher sends, from the form's fields (specs/070). No owner - the server reads it from the
 * token - and nothing a role cannot send: free delivery and "new customers" only for the platform, "first order
 * in my shop" only for a shop (research D2).
 */
export function toNewVoucher(form: {
  platform: boolean
  code: string
  name: string
  benefit: Benefit
  percent: string
  rows: AmountRow[]
  startsAt: string
  endsAt: string
  totalLimit: string
  perCustomerLimit: string
  newCustomer: boolean
  firstInShop: boolean
  minQuantity: string
  products: string[]
}): NewVoucher {
  const conditions: VoucherCondition[] = []
  if (form.platform && form.newCustomer) conditions.push({ type: 'NewCustomer', value: null })
  if (!form.platform && form.firstInShop) conditions.push({ type: 'FirstOrderInShop', value: null })
  if (number(form.minQuantity) !== null) conditions.push({ type: 'MinQuantity', value: number(form.minQuantity) })

  return {
    code: form.code.trim().toUpperCase(),
    name: form.name.trim(),
    benefit: form.benefit,
    percent: form.benefit === 'Percent' ? number(form.percent) : null,
    startsAt: iso(form.startsAt),
    endsAt: iso(form.endsAt),
    totalLimit: number(form.totalLimit),
    perCustomerLimit: number(form.perCustomerLimit),
    amounts: form.rows.map((row) => ({
      currency: row.currency,
      fixedValue: form.benefit === 'FixedAmount' ? number(row.fixedValue) : null,
      maxDiscount: form.benefit === 'FixedAmount' ? null : number(row.maxDiscount),
      minSubtotal: number(row.minSubtotal),
    })),
    conditions,
    targets: form.benefit === 'FreeShipping' ? [] : form.products.map((id) => ({ type: 'Product', id })),
  }
}
