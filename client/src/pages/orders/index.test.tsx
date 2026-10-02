import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Order } from '@/services/order'
import type { OrderSummary } from '@/services/order/types'
import { Product } from '@/services/product'
import { renderAsCustomer } from '@/test/render'
import { OrdersPage } from '.'

const order = (over: Partial<OrderSummary>): OrderSummary => ({
  orderId: '01a0f63a-c642-7000-8beb-266fb146b397', totalAmount: 20_383_000, status: 'Paid', failureReason: null, itemCount: 1,
  createdAt: '2026-10-01T08:00:00Z', updatedAt: '2026-10-01T08:00:00Z', currency: 'VND', language: 'en',
  shipmentCount: 1, shipmentsShipped: 0, lines: [{ productId: 'p1', variantId: 'v1', productName: 'Canon EOS R50' }], ...over,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Product, 'get').mockRejectedValue(new Error('not needed here'))
})

describe('OrdersPage (specs/132, #248)', () => {
  it('names each order by its reference, its products and its state - not by a timestamp', async () => {
    vi.spyOn(Order, 'listMine').mockResolvedValue({
      items: [order({}), order({ orderId: '01a0f6b3-0000-7000-8000-000000000002', status: 'Shipped', lines: [{ productId: 'p2', variantId: null, productName: 'RF 50mm' }] })],
      page: 1, pageSize: 12, totalCount: 2,
    } as never)
    renderAsCustomer(<OrdersPage />, '/orders')

    const first = await screen.findByRole('link', { name: /01a0f63a/ })
    expect(first).toHaveAttribute('href', '/orders/01a0f63a-c642-7000-8beb-266fb146b397')
    expect(within(first).getByText('Canon EOS R50')).toBeInTheDocument()
    expect(within(first).getByText('Paid')).toBeInTheDocument()
    expect(within(screen.getByRole('link', { name: /01a0f6b3/ })).getByText('Shipped')).toBeInTheDocument()
  })

  it('still says why a failed order failed', async () => {
    vi.spyOn(Order, 'listMine').mockResolvedValue({
      items: [order({ status: 'Failed', failureReason: 'Insufficient stock for product p1' })], page: 1, pageSize: 12, totalCount: 1,
    } as never)
    renderAsCustomer(<OrdersPage />, '/orders')

    expect(await screen.findByText(/ran out of stock/)).toBeInTheDocument()
  })
})
