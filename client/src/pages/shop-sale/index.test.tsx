import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { AxiosError } from 'axios'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Order } from '@/services/order'
import type { Sale } from '@/services/order/types'
import { ShopSalePage } from '.'

function renderAt(id: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[`/shop/sales/${id}`]}>
        <Routes>
          <Route path="/shop/sales/:id" element={<ShopSalePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const shipped: Sale = {
  orderId: 'o-1',
  status: 'Shipped',
  createdAt: '2026-09-23T08:14:02Z',
  updatedAt: '2026-09-23T09:30:11Z',
  currency: 'USD',
  language: 'en',
  subtotal: 2798,
  items: [{
    productId: 'p1', productName: 'Sony A7 IV', variantId: 'v1', sku: 'SONY-A7M4',
    optionSummary: 'Kit: Body only', quantity: 2, unitPrice: 1399, totalPrice: 2798, taxAmount: 279.8,
  }],
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopSalePage', () => {
  it('asks for the sale named in the address, and shows the seller its lines', async () => {
    const sale = vi.spyOn(Order, 'sale').mockResolvedValue(shipped)
    renderAt('o-1')

    expect(await screen.findByText('Sony A7 IV')).toBeInTheDocument()
    expect(sale).toHaveBeenCalledWith('o-1')
    expect(screen.getByText('Kit: Body only')).toBeInTheDocument()
    expect(screen.getByText('Shipped')).toBeInTheDocument()
  })

  it('labels the subtotal as the seller part, in the order currency', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(shipped)
    renderAt('o-1')

    const label = await screen.findByText(/Your lines/)
    expect(label).toHaveTextContent('$2,798.00')
    // Said on the page, because a subtotal next to "your lines" otherwise reads as the whole order.
    expect(screen.getByText(/Delivery and tax belong to the whole order/)).toBeInTheDocument()
  })

  /**
   * "Not your sale" and "no such order" are the same 404 on purpose (specs/034 research D5). The page
   * repeats what the server said instead of guessing which of the two it was.
   */
  it('shows a refusal in the server words', async () => {
    const refusal = new AxiosError('failed')
    refusal.response = {
      status: 404,
      data: { status: 404, detail: 'Sale not found.' },
      statusText: '',
      headers: {},
      config: { headers: {} as never },
    }
    vi.spyOn(Order, 'sale').mockRejectedValue(refusal)
    renderAt('someone-elses')

    expect(await screen.findByRole('alert')).toHaveTextContent('Sale not found.')
    expect(screen.getByRole('link', { name: /Sales/ })).toHaveAttribute('href', '/shop/sales')
  })
})
