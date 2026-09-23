import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Admin } from '@/services/admin'
import type { Order, Shipment } from '@/services/order/types'
import { refusal } from '@/test/refusal'
import { renderAsAdmin } from '@/test/render'
import { AdminOrderPage } from '.'

const address = {
  recipientName: 'Lan Pham', line1: '12 Ly Thuong Kiet', line2: null, city: 'Ha Noi', region: null,
  postalCode: '100000', country: 'VN', phone: '+84 912 345 678',
}

function order(shipments: Shipment[]): Order {
  return {
    orderId: 'o-1', status: 'Paid', failureReason: null, createdAt: '2026-09-23T08:00:00Z', updatedAt: '',
    items: [{
      productId: 'p', productName: 'Ricoh GR III', variantId: null, sku: null, optionSummary: null,
      quantity: 1, unitPrice: 1, totalPrice: 1, taxAmount: null,
    }],
    shippingAddress: address, shippingOption: null, trackingReference: null, shipments,
    currency: 'VND', subtotal: 1, shippingPrice: 0, taxTotal: 0, discountTotal: 0, taxRate: 0, totalAmount: 1,
  }
}

const shop = (status: string): Shipment => ({ status, trackingReference: null, items: ['Ricoh GR III'], sellerName: null, isShop: true })
const hers: Shipment = { status: 'Paid', trackingReference: null, items: ['Viltrox 56mm'], sellerName: 'Mai', isShop: false }

function renderAt(id = 'o-1') {
  return renderAsAdmin(
    <Routes>
      <Route path="/admin/orders/:id" element={<AdminOrderPage />} />
    </Routes>,
    `/admin/orders/${id}`,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminOrderPage', () => {
  it('reads the order through the staff route and shows what to pack and where', async () => {
    const read = vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Paid'), hers]))
    renderAt()

    expect(await screen.findByText('To pack')).toBeInTheDocument()
    expect(read).toHaveBeenCalledWith('o-1')
    expect(screen.getByText('Lan Pham')).toBeInTheDocument()
  })

  /** The shop parcel's step, not the order's - and the call is the staff one, for this order. */
  it('starts preparing the shop parcel', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Paid'), hers]))
    const prepare = vi.spyOn(Admin, 'prepare').mockResolvedValue(order([shop('Preparing'), hers]))
    renderAt()

    await userEvent.click(await screen.findByRole('button', { name: /Start preparing/ }))

    await waitFor(() => expect(prepare).toHaveBeenCalledWith('o-1'))
  })

  it('ships it with the tracking reference typed in', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Preparing')]))
    const ship = vi.spyOn(Admin, 'ship').mockResolvedValue(order([shop('Shipped')]))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Mark as shipped/ }))
    await user.type(await screen.findByLabelText('Tracking reference'), 'VNPOST-SHOP-1')
    await user.click(screen.getAllByRole('button', { name: /Mark as shipped/ }).at(-1)!)

    await waitFor(() => expect(ship).toHaveBeenCalledWith('o-1', 'VNPOST-SHOP-1'))
  })

  /** Every parcel is a seller's: say so, and offer staff nothing to press. */
  it('offers no step on an order with none of the shop goods', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([hers]))
    renderAt()

    expect(await screen.findByText(/each seller sends their own parcel/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Start preparing/ })).not.toBeInTheDocument()
  })

  it('shows a refusal in the server words', async () => {
    vi.spyOn(Admin, 'order').mockRejectedValue(refusal(404, 'Order not found.'))
    renderAt('missing')

    expect(await screen.findByRole('alert')).toHaveTextContent('Order not found.')
  })
})
