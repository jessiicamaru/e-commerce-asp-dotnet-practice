import { describe, expect, it } from 'vitest'
import { initialsOf } from '@/components/layout/user-menu/initials'
import { groupDigits, pendingChanges } from '@/components/seller/variant-editor/pending-changes'
import { countryChoices } from '@/utils/address/countries'
import { summariseSales } from '@/utils/seller'
import { fold, looselyIncludes } from '@/utils/shared'
import { stockLevel, sumStock } from '@/utils/stock'
import { noEarnings } from '@/test/fixtures'

describe('looselyIncludes', () => {
  /** "đ" is its own letter, not an accent: NFD leaves it alone, so it is replaced by hand. */
  it('finds Vietnamese text typed without accents', () => {
    expect(fold('Máy ảnh Đà Nẵng')).toBe('may anh da nang')
    expect(looselyIncludes('Máy ảnh không gương lật', 'may anh')).toBe(true)
    expect(looselyIncludes('Đà Nẵng', 'da nang')).toBe(true)
    expect(looselyIncludes('Phụ kiện', 'ong kinh')).toBe(false)
  })
})

describe('summariseSales', () => {
  const sale = (subtotal: number, currency: string, units = 1) => ({
    orderId: 'o', status: 'Paid', createdAt: '', updatedAt: '', lineCount: 1, units, subtotal, currency, ...noEarnings,
  })

  /** The shop converts nothing (specs/022): dong and dollars are two totals, never one. */
  it('keeps a total per currency', () => {
    const summary = summariseSales([sale(5190000, 'VND'), sale(2380000, 'VND', 2), sale(54.99, 'USD')])

    expect(summary.revenue).toEqual({ VND: 7570000, USD: 54.99 })
    expect(summary.units).toBe(4)
    expect(summary.orders).toBe(3)
  })

  it('adds cents without the floating-point tail a person would see', () => {
    expect(summariseSales([sale(19.99, 'USD'), sale(29.99, 'USD')]).revenue.USD).toBe(49.98)
  })
})

describe('stock', () => {
  it('grades a number', () => {
    expect(stockLevel(0)).toBe('out')
    expect(stockLevel(2)).toBe('low')
    expect(stockLevel(12)).toBe('in')
  })

  /** A stock row that has not arrived yet is "unknown", not zero - zero would read as sold out. */
  it('does not turn a missing number into zero', () => {
    expect(stockLevel(null)).toBe('unknown')
    expect(sumStock([null, null]).available).toBeNull()
  })

  it('sums the variants that answered and says how many did', () => {
    const row = (onHand: number, reserved: number) => ({
      productId: 'v', sku: 'v', quantityOnHand: onHand, quantityReserved: reserved, quantityAvailable: onHand - reserved,
    })
    expect(sumStock([row(6, 1), null, row(4, 0)])).toEqual({ known: 2, total: 3, onHand: 10, reserved: 1, available: 9 })
  })
})

describe('pendingChanges', () => {
  const current = { prices: { VND: 5190000, USD: null }, onHand: 3 }

  /** Re-sending an untouched price could overwrite a change somebody else made a minute ago. */
  it('sends only what differs from the server', () => {
    expect(pendingChanges({ prices: { VND: '5190000', USD: '219' }, onHand: '3' }, current)).toEqual({
      prices: [{ currency: 'USD', amount: 219 }],
      onHand: null,
    })
  })

  it('reads a price typed with separators', () => {
    expect(pendingChanges({ prices: { VND: '5,290,000' }, onHand: undefined }, current).prices).toEqual([
      { currency: 'VND', amount: 5290000 },
    ])
  })

  it('ignores what does not parse, and a negative or fractional stock', () => {
    expect(pendingChanges({ prices: { USD: 'abc' }, onHand: '-2' }, current)).toEqual({ prices: [], onHand: null })
    expect(pendingChanges({ prices: {}, onHand: '2.5' }, current).onHand).toBeNull()
    expect(pendingChanges({ prices: {}, onHand: '9' }, current).onHand).toBe(9)
  })
})

describe('groupDigits', () => {
  it('groups by spaces and keeps the decimal point', () => {
    expect(groupDigits(15490000)).toBe('15 490 000')
    expect(groupDigits(119.95)).toBe('119.95')
    expect(groupDigits(549)).toBe('549')
  })

  /** What the box shows must be what a save reads back - or saving an untouched box would change it. */
  it('is read back as the same number, so an untouched box sends nothing', () => {
    const shown = groupDigits(15490000)
    expect(pendingChanges({ prices: { VND: shown }, onHand: undefined }, { prices: { VND: 15490000 }, onHand: 3 }).prices).toEqual([])
  })
})

describe('initialsOf', () => {
  it('takes one letter from each name, accents and all', () => {
    expect(initialsOf({ firstName: 'Mai', lastName: 'Trần', email: 'm@x' })).toBe('MT')
    expect(initialsOf({ firstName: '', lastName: '', email: 'lan@demo.test' })).toBe('L')
  })
})

describe('countryChoices', () => {
  /** Most of this shop's customers are here; it should not be under "Vanuatu". */
  it('puts Vietnam first and names countries in the reader language', () => {
    const vi = countryChoices('vi')
    const en = countryChoices('en')

    expect(vi[0]).toMatchObject({ value: 'VN', hint: 'VN' })
    expect(en.find((c) => c.value === 'GB')?.label).toMatch(/United Kingdom/)
    expect(new Set(en.map((c) => c.value)).size).toBe(en.length)
  })
})
