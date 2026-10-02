import { render, screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import type { OrderLine } from '@ecommerce/core/services/order/types'
import { OrderLines } from '.'

const line = (overrides: Partial<OrderLine> = {}): OrderLine => ({
  productId: 'p1', productName: 'Viltrox AF 56mm F1.4', variantId: 'v1', sku: 'VIL-56', optionSummary: null,
  quantity: 1, unitPrice: 5190000, totalPrice: 5190000, taxAmount: 519000, ...overrides,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('OrderLines', () => {
  /** specs/036: an order from one seller says who sold it even though it has no parcels list. */
  it('says which shop sold a line', () => {
    render(<OrderLines items={[line({ sellerName: 'Mai Lens' })]} currency="VND" />)

    expect(screen.getByText('Sold by Mai Lens')).toBeInTheDocument()
  })

  it('says nothing for a line with no recorded shop', () => {
    render(<OrderLines items={[line({ sellerName: null })]} currency="VND" />)

    expect(screen.queryByText(/Sold by/)).not.toBeInTheDocument()
  })

  /** specs/118 (#239): two columns at every width - the quantity under the details, the total on its own. */
  it('puts quantity × price under the details and the total, with its discount, in a column of its own', () => {
    render(<OrderLines items={[line({ quantity: 2, unitPrice: 600000, totalPrice: 1200000, discount: 100000 })]} currency="VND" />)

    const [item] = screen.getAllByRole('listitem')
    expect(within(item).getByTestId('order-line-quantity')).toHaveTextContent(/^2 × .*600,000$/)
    const total = within(item).getByTestId('order-line-total')
    expect(total).toHaveTextContent(/1,200,000/)
    expect(total).toHaveTextContent(/-.*100,000/)
    expect(total).not.toHaveTextContent(/600,000/)
    expect(total.className).toContain('whitespace-nowrap')
  })

  it('lets long details break instead of widening the line', () => {
    render(<OrderLines items={[line({ sku: 'E2ECAMERA1790837359318' })]} currency="VND" />)

    const details = screen.getByText('SKU E2ECAMERA1790837359318').parentElement!
    expect(details.className).toContain('min-w-0')
    expect(details.className).toContain('[overflow-wrap:anywhere]')
    expect(screen.getByRole('listitem').className).toContain('grid-cols-[minmax(0,1fr)_auto]')
  })

  it('is translated', async () => {
    await i18n.changeLanguage('vi')
    render(<OrderLines items={[line({ sellerName: 'Mai Lens' })]} currency="VND" />)

    expect(screen.getByText('Bán bởi Mai Lens')).toBeInTheDocument()
    await i18n.changeLanguage('en')
  })
})
