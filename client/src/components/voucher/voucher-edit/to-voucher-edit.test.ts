import { describe, expect, it } from 'vitest'
import type { VoucherSummary } from '@/services/voucher/types'
import { editFields, toLocalInput, toVoucherEdit } from './to-voucher-edit'

const voucher: VoucherSummary = {
  id: 'v-1', code: 'MAI10', name: 'Mai ten', isPlatform: false, benefit: 'Percent', percent: 10, status: 'Active',
  startsAt: '2026-09-26T00:00:00Z', endsAt: '2026-10-31T16:59:00Z', totalLimit: 100, usedCount: 3, perCustomerLimit: null,
  amounts: [
    { currency: 'USD', fixedValue: null, maxDiscount: 5, minSubtotal: null },
    { currency: 'VND', fixedValue: null, maxDiscount: 100_000, minSubtotal: 500_000 },
  ],
  conditions: [{ type: 'MinQuantity', value: 2 }], targets: [], createdAt: '', isPublic: true,
}

describe('editFields and toVoucherEdit (specs/113)', () => {
  it('prefills the form with what the voucher says now, the end in local time', () => {
    const fields = editFields(voucher)

    expect(fields).toEqual({
      name: 'Mai ten', endsAt: toLocalInput('2026-10-31T16:59:00Z'), totalLimit: '100', perCustomerLimit: '',
      minSubtotals: { USD: '', VND: '500000' }, minQuantity: '2', isPublic: true,
    })
  })

  it('sends every term as its new value - an emptied box is none', () => {
    const fields = { ...editFields(voucher), name: '  Autumn  ', totalLimit: '', minSubtotals: { USD: '20', VND: '' }, minQuantity: '3' }

    expect(toVoucherEdit(fields, voucher)).toEqual({
      name: 'Autumn', endsAt: new Date(fields.endsAt).toISOString(), totalLimit: null, perCustomerLimit: null,
      minSubtotals: [{ currency: 'USD', minSubtotal: 20 }, { currency: 'VND', minSubtotal: null }], minQuantity: 3, isPublic: true,
    })
  })

  it('sends no minimum quantity for a voucher without one, and only its own currencies', () => {
    const plain = { ...voucher, conditions: [], amounts: [voucher.amounts[1]] }
    const edit = toVoucherEdit({ ...editFields(plain), minQuantity: '5', minSubtotals: { VND: '1', EUR: '9' } }, plain)

    expect(edit.minQuantity).toBeNull()
    expect(edit.minSubtotals).toEqual([{ currency: 'VND', minSubtotal: 1 }])
  })

  it('clears the end when the box is emptied', () => {
    expect(toVoucherEdit({ ...editFields(voucher), endsAt: '' }, voucher).endsAt).toBeNull()
  })
})
