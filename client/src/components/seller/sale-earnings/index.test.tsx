import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import type { Sale } from '@/services/order/types'
import { noEarnings } from '@/test/fixtures'
import { SaleEarnings } from '.'
import { earningState } from './state'

function sale(overrides: Partial<Sale> = {}): Sale {
  return {
    orderId: 'o-1', status: 'Paid', createdAt: '', updatedAt: '', items: [], subtotal: 1_200_000,
    currency: 'VND', language: 'vi', trackingReference: null, shippingAddress: null,
    goodsTotal: 1_200_000, commission: 120_000, shippingShare: 15_000, payout: 1_095_000, paidOut: false,
    ...overrides,
  }
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('earningState', () => {
  it('follows the money: on the way, due once sent, paid out once settled', () => {
    expect(earningState({ payout: 10, paidOut: false, status: 'Paid' })).toBe('onTheWay')
    expect(earningState({ payout: 10, paidOut: false, status: 'Preparing' })).toBe('onTheWay')
    expect(earningState({ payout: 10, paidOut: false, status: 'Shipped' })).toBe('due')
    expect(earningState({ payout: 10, paidOut: true, status: 'Shipped' })).toBe('paidOut')
  })

  /** A null payout is "never recorded", not "zero" - the one state that must not be read as money. */
  it('reads a missing payout as unrecorded, whatever else the sale says', () => {
    expect(earningState({ payout: null, paidOut: false, status: 'Shipped' })).toBe('unrecorded')
  })
})

describe('SaleEarnings', () => {
  it('shows goods, less commission, plus the delivery share, and what that comes to', () => {
    render(<SaleEarnings sale={sale()} />)

    expect(screen.getByText('Goods (before tax)').nextElementSibling).toHaveTextContent('1,200,000')
    expect(screen.getByText('Marketplace commission').nextElementSibling).toHaveTextContent('− ₫120,000')
    expect(screen.getByText('Your share of delivery').nextElementSibling).toHaveTextContent('+ ₫15,000')
    expect(screen.getAllByText('You receive').at(-1)!.nextElementSibling).toHaveTextContent('₫1,095,000')
    expect(screen.getByText('On the way')).toBeInTheDocument()
  })

  /** Every number is the server's, in the ORDER's currency - never recomputed or converted here. */
  it('shows the amounts the server sent, in the order currency', () => {
    render(<SaleEarnings sale={sale({ currency: 'USD', goodsTotal: 10, commission: 1, shippingShare: 1.68, payout: 10.68 })} />)

    expect(screen.getAllByText('You receive').at(-1)!.nextElementSibling).toHaveTextContent('$10.68')
  })

  it('says when the shop has paid it out', () => {
    render(<SaleEarnings sale={sale({ status: 'Shipped', paidOut: true })} />)

    expect(screen.getByText('Paid out')).toBeInTheDocument()
  })

  /** An old order says why there is no number, rather than showing zeros that read as "you earned nothing". */
  it('explains an order whose terms were never recorded, and shows no amounts', () => {
    render(<SaleEarnings sale={sale({ ...noEarnings })} />)

    expect(screen.getByText(/placed before the shop recorded commission/)).toBeInTheDocument()
    expect(screen.queryByText(/₫/)).not.toBeInTheDocument()
  })
})
