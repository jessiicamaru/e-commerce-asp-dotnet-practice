import { fireEvent, screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Product } from '@ecommerce/core/services/product'
import { renderAsCustomer } from '@ecommerce/core/test/render'
import { orderReference } from '@ecommerce/core/utils/order'
import { OrderReference } from '../order-reference'
import { OrderStatusChip } from '../order-status-chip'
import { OrderRow } from '.'

const lines = [
  { productId: 'p1', variantId: 'v1', productName: 'Canon EOS R50' },
  { productId: 'p2', variantId: 'v2', productName: 'RF 50mm' },
  { productId: 'p3', variantId: null, productName: 'Battery' },
]

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Product, 'get').mockResolvedValue({ id: 'p1', name: 'Canon EOS R50', imageUrl: '/api/products/p1/image?v=1', variants: [] } as never)
})

describe('OrderRow (specs/132, #248)', () => {
  it('is one link naming the order by its short reference, its products and its state', () => {
    renderAsCustomer(
      <OrderRow to="/orders/01a0f63a-0000-7000-8000-000000000001" orderId="01a0f63a-0000-7000-8000-000000000001" lines={lines}
        lineCount={5} createdAt="2026-10-01T08:00:00Z" status="Shipped" amount={20_383_000} currency="VND" />,
    )

    const row = screen.getByRole('link')
    expect(row).toHaveAttribute('href', '/orders/01a0f63a-0000-7000-8000-000000000001')
    expect(within(row).getByText('01a0f63a')).toBeInTheDocument()
    expect(within(row).getByText(/Canon EOS R50 · RF 50mm · Battery/)).toBeInTheDocument()
    expect(within(row).getByText('+ 2 more')).toBeInTheDocument()
    expect(within(row).getByText('Shipped')).toBeInTheDocument()
  })

  it('shows the first product picture, and the lens tile when the product is gone', async () => {
    const { unmount } = renderAsCustomer(
      <OrderRow to="/orders/o1" orderId="o1" lines={lines} lineCount={3} createdAt="2026-10-01T08:00:00Z" status="Paid" amount={1} currency="VND" />,
    )
    expect(await screen.findByRole('img', { name: 'Canon EOS R50' })).toHaveAttribute('src', '/api/products/p1/image?v=1')
    unmount()

    vi.mocked(Product.get).mockRejectedValue(new Error('404'))
    renderAsCustomer(
      <OrderRow to="/orders/o1" orderId="o1" lines={lines} lineCount={3} createdAt="2026-10-01T08:00:00Z" status="Paid" amount={1} currency="VND" />,
    )
    await new Promise((resolve) => setTimeout(resolve, 20))
    expect(screen.queryByRole('img', { name: 'Canon EOS R50' })).not.toBeInTheDocument()
  })
})

describe('the short reference (specs/132)', () => {
  it('is the eight characters the notices use', () => {
    expect(orderReference('01a0f63a-c642-7000-8beb-266fb146b397')).toBe('01a0f63a')
  })

  it('is copied as such', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
    renderAsCustomer(<OrderReference orderId="01a0f63a-c642-7000-8beb-266fb146b397" />)

    // fireEvent, not userEvent: userEvent.setup() puts its own clipboard in place of this one.
    fireEvent.click(screen.getByRole('button', { name: 'Copy the order reference' }))
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(writeText).toHaveBeenCalledWith('01a0f63a')
    expect(screen.getByText('Order 01a0f63a')).toBeInTheDocument()
  })
})

describe('OrderStatusChip (specs/132)', () => {
  it('says how many parcels have gone while some have not', () => {
    renderAsCustomer(<OrderStatusChip status="Preparing" shipped={1} parcels={2} />)
    expect(screen.getByText('1 of 2 shipped')).toBeInTheDocument()
  })

  it('says the state once every parcel is the same, and in Vietnamese', async () => {
    await i18n.changeLanguage('vi')
    renderAsCustomer(<OrderStatusChip status="Shipped" shipped={2} parcels={2} />)
    expect(screen.getByText('Đã gửi')).toBeInTheDocument()
  })
})
