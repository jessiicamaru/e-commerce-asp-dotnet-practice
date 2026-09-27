import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Order } from '@/services/order'
import type { ParcelReturn, ReturnPage } from '@/services/order/types'
import { renderAsSeller } from '@/test/render'
import { ShopReturnsPage } from '.'

const scratched: ParcelReturn = {
  id: 'r1', orderId: '0199abcd-1111-7000-8000-000000000001', shipmentId: 'p1', isShop: false, status: 'Requested',
  reason: 'Scratched lens', decisionReason: null, trackingReference: null, requestedAt: '2026-09-27T08:00:00Z',
  decidedAt: null, sentBackAt: null, receivedAt: null, refundAmount: null,
}
const page = (...items: ParcelReturn[]): ReturnPage => ({ items, page: 1, pageSize: PAGE_SIZE, totalCount: items.length })

function renderPage(path = '/shop/returns') {
  return renderAsSeller(
    <Routes>
      <Route path="/shop/returns" element={<ShopReturnsPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopReturnsPage (specs/108)', () => {
  /** It opens on what asks for the seller's decision, and each row leads to the sale where the steps are. */
  it('opens on the returns to answer, each linking to its sale', async () => {
    const list = vi.spyOn(Order, 'saleReturns').mockResolvedValue(page(scratched))
    renderPage()

    const link = await screen.findByRole('link', { name: /Order 0199abcd/ })
    expect(link).toHaveAttribute('href', `/shop/sales/${scratched.orderId}`)
    expect(link).toHaveTextContent('Scratched lens')
    expect(list).toHaveBeenCalledWith('Requested', 1, PAGE_SIZE)
    expect(screen.getByRole('tab', { name: 'To answer' })).toHaveAttribute('aria-selected', 'true')
  })

  it('asks for another state when its tab is chosen', async () => {
    const list = vi.spyOn(Order, 'saleReturns').mockResolvedValue(page())
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('tab', { name: 'Coming back' }))

    await waitFor(() => expect(list).toHaveBeenCalledWith('SentBack', 1, PAGE_SIZE))
    expect(await screen.findByText('Nothing here.')).toBeInTheDocument()
  })

  it('shows the reference a parcel was sent back with', async () => {
    vi.spyOn(Order, 'saleReturns').mockResolvedValue(page({ ...scratched, status: 'SentBack', trackingReference: 'VNPOST-BACK' }))
    renderPage('/shop/returns?status=SentBack')

    expect(await screen.findByText('Sent back with VNPOST-BACK')).toBeInTheDocument()
  })
})
