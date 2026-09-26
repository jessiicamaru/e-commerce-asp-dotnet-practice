import { describe, expect, it } from 'vitest'
import { toNewVoucher } from './to-new-voucher'

const base = {
  platform: false, code: ' shop-10 ', name: ' Ten off ', benefit: 'Percent' as const, percent: '10',
  rows: [{ currency: 'VND', fixedValue: '50000', maxDiscount: '100000', minSubtotal: '' }],
  startsAt: '', endsAt: '', totalLimit: '', perCustomerLimit: '1',
  newCustomer: true, firstInShop: true, minQuantity: '', products: ['p1'],
}

describe('toNewVoucher (specs/070)', () => {
  it("sends a seller's percentage voucher: upper-case code, a cap and no fixed value, their first-order rule", () => {
    expect(toNewVoucher(base)).toEqual({
      code: 'SHOP-10', name: 'Ten off', benefit: 'Percent', percent: 10, startsAt: null, endsAt: null,
      totalLimit: null, perCustomerLimit: 1,
      amounts: [{ currency: 'VND', fixedValue: null, maxDiscount: 100_000, minSubtotal: null }],
      // Research D2: "new customers" is the platform's - a shop never sends it, even if the box was ticked.
      conditions: [{ type: 'FirstOrderInShop', value: null }],
      targets: [{ type: 'Product', id: 'p1' }],
    })
  })

  it("sends the platform's new-customer rule and never a shop's first-order one", () => {
    expect(toNewVoucher({ ...base, platform: true }).conditions).toEqual([{ type: 'NewCustomer', value: null }])
  })

  it('a fixed amount sends its value and no cap; a minimum quantity is a condition', () => {
    const fixed = toNewVoucher({ ...base, benefit: 'FixedAmount', minQuantity: '2' })

    expect(fixed.percent).toBeNull()
    expect(fixed.amounts[0]).toEqual({ currency: 'VND', fixedValue: 50_000, maxDiscount: null, minSubtotal: null })
    expect(fixed.conditions).toContainEqual({ type: 'MinQuantity', value: 2 })
  })

  it('free delivery applies to the delivery, so it names no products', () => {
    expect(toNewVoucher({ ...base, platform: true, benefit: 'FreeShipping' }).targets).toEqual([])
  })

  it('dates become ISO instants', () => {
    const dated = toNewVoucher({ ...base, startsAt: '2026-10-01T09:00', endsAt: '2026-10-31T23:59' })

    expect(dated.startsAt).toBe(new Date('2026-10-01T09:00').toISOString())
    expect(dated.endsAt).toBe(new Date('2026-10-31T23:59').toISOString())
  })
})
