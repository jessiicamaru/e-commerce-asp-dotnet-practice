import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import type { Totals } from '@ecommerce/core/services/order/types'
import { OrderTotals } from '.'

const totals = (overrides: Partial<Totals> = {}): Totals =>
  ({
    currency: 'VND', subtotal: 1250000, shippingPrice: 30000, taxTotal: 128000, taxRate: 0.1, discountTotal: 0,
    totalAmount: 1408000, vouchers: [], ...overrides,
  }) as Totals

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('OrderTotals', () => {
  it('names each part with its amount', () => {
    render(<OrderTotals totals={totals()} shippingName="Standard delivery" />)

    expect(screen.getByText('Delivery · Standard delivery')).toBeInTheDocument()
    expect(screen.getByText('Tax (10%)')).toBeInTheDocument()
  })

  /** specs/118 (#239): the total is one row, so its rule runs under the label and the amount without a gap. */
  it('keeps the total label and amount in one ruled row', () => {
    render(<OrderTotals totals={totals()} />)

    const row = screen.getByTestId('order-total-row')
    expect(row).toHaveTextContent(/^Total.*1,408,000$/)
    expect(row.className).toContain('border-t')
    expect(row.className).toContain('col-span-2')
    expect(screen.getByText('Total').className).not.toContain('border-t')
  })
})
