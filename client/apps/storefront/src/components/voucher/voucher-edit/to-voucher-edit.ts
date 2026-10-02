import type { VoucherEdit, VoucherSummary } from '@ecommerce/core/services/voucher/types'

/** The edit form's fields: text, as the inputs hold it. */
export interface EditFields {
  name: string
  endsAt: string
  totalLimit: string
  perCustomerLimit: string
  minSubtotals: Record<string, string>
  minQuantity: string
  isPublic: boolean
}

const text = (value: number | null) => (value === null ? '' : String(value))
const number = (value: string): number | null => (value.trim() === '' ? null : Number(value))
const pad = (n: number) => String(n).padStart(2, '0')

/** An instant as a `datetime-local` input shows it - in the reader's own time zone. */
export function toLocalInput(iso: string | null): string {
  if (!iso) return ''
  const at = new Date(iso)
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`
}

/** The form, prefilled with what the voucher says now (specs/113). */
export function editFields(voucher: VoucherSummary): EditFields {
  return {
    name: voucher.name,
    endsAt: toLocalInput(voucher.endsAt),
    totalLimit: text(voucher.totalLimit),
    perCustomerLimit: text(voucher.perCustomerLimit),
    minSubtotals: Object.fromEntries(voucher.amounts.map((a) => [a.currency, text(a.minSubtotal)])),
    minQuantity: text(voucher.conditions.find((c) => c.type === 'MinQuantity')?.value ?? null),
    isPublic: voucher.isPublic ?? false,
  }
}

/**
 * What an edit sends (specs/113): every term that may change, as its new value - an emptied box is "none". Only the
 * voucher's own currencies, and a minimum quantity only when it has one: the server refuses anything else.
 */
export function toVoucherEdit(fields: EditFields, voucher: VoucherSummary): VoucherEdit {
  const hasMinQuantity = voucher.conditions.some((c) => c.type === 'MinQuantity')
  return {
    name: fields.name.trim(),
    endsAt: fields.endsAt ? new Date(fields.endsAt).toISOString() : null,
    totalLimit: number(fields.totalLimit),
    perCustomerLimit: number(fields.perCustomerLimit),
    minSubtotals: voucher.amounts.map((a) => ({ currency: a.currency, minSubtotal: number(fields.minSubtotals[a.currency] ?? '') })),
    minQuantity: hasMinQuantity ? number(fields.minQuantity) : null,
    isPublic: fields.isPublic,
  }
}
