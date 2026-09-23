import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import type { OrderLine } from '@/services/order/types'
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

  it('is translated', async () => {
    await i18n.changeLanguage('vi')
    render(<OrderLines items={[line({ sellerName: 'Mai Lens' })]} currency="VND" />)

    expect(screen.getByText('Bán bởi Mai Lens')).toBeInTheDocument()
    await i18n.changeLanguage('en')
  })
})
