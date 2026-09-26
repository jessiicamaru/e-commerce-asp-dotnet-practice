import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import type { VoucherSummary } from '@/services/voucher/types'
import { describeBenefit, describeRules, describeUses } from './describe'

const t = i18n.getFixedT('en', 'vouchers')

const voucher = (overrides: Partial<VoucherSummary>): VoucherSummary => ({
  id: 'v', code: 'SALE30', name: 'Sale', isPlatform: true, benefit: 'Percent', percent: 30, status: 'Active',
  startsAt: '2026-09-26T00:00:00Z', endsAt: null, totalLimit: null, usedCount: 0, perCustomerLimit: null,
  amounts: [{ currency: 'VND', fixedValue: null, maxDiscount: 200_000, minSubtotal: 1_000_000 }],
  conditions: [], targets: [], createdAt: '', ...overrides,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('describeBenefit (specs/070)', () => {
  it("words the user's example: thirty percent, capped, on orders from a million", () => {
    expect(describeBenefit(t, voucher({}))).toEqual(['30% off · up to ₫200,000 · on orders from ₫1,000,000'])
  })

  /** One line per currency, each its own money - never one converted from the other. */
  it('says each currency on its own line', () => {
    const fixed = voucher({
      benefit: 'FixedAmount', percent: null,
      amounts: [{ currency: 'VND', fixedValue: 50_000, maxDiscount: null, minSubtotal: null }, { currency: 'USD', fixedValue: 2, maxDiscount: null, minSubtotal: 20 }],
    })

    expect(describeBenefit(t, fixed)).toEqual(['₫50,000 off', '$2.00 off · on orders from $20.00'])
  })

  it('words free delivery and its cap', () => {
    expect(describeBenefit(t, voucher({ benefit: 'FreeShipping', percent: null, amounts: [{ currency: 'VND', fixedValue: null, maxDiscount: 30_000, minSubtotal: null }] })))
      .toEqual(['Free delivery · up to ₫30,000'])
  })
})

describe('describeRules and describeUses', () => {
  it('names each condition and whether it is on everything or on some products', () => {
    expect(describeRules(t, voucher({ conditions: [{ type: 'NewCustomer', value: null }, { type: 'MinQuantity', value: 2 }] })))
      .toEqual(['New customers only', 'At least 2 items', 'Everything'])
    expect(describeRules(t, voucher({ targets: [{ type: 'Product', id: 'a' }, { type: 'Product', id: 'b' }] }))).toEqual(['2 products'])
  })

  it('counts the uses against the limits', () => {
    expect(describeUses(t, voucher({ usedCount: 3, totalLimit: 100, perCustomerLimit: 1 }))).toBe('Used 3 of 100 · once per customer')
    expect(describeUses(t, voucher({ usedCount: 1 }))).toBe('Used once')
  })
})
