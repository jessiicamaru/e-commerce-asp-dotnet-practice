import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Order } from '@/services/order'
import type { Order as OrderModel, Shipment } from '@/services/order/types'
import { refusal } from '@/test/refusal'
import { renderAsSeller } from '@/test/render'
import { OrderPage } from '.'

const part = (status: string, sellerName: string | null): Shipment => ({
  status, trackingReference: null, items: ['Camera'], sellerName, isShop: sellerName === null,
})

function order(overrides: Partial<OrderModel> = {}): OrderModel {
  return {
    orderId: 'o-1', status: 'Paid', failureReason: null, createdAt: '2026-09-23T08:00:00Z', updatedAt: '',
    items: [{
      productId: 'p', productName: 'Ricoh GR III', variantId: null, sku: null, optionSummary: null,
      quantity: 1, unitPrice: 1, totalPrice: 1, taxAmount: null,
    }],
    shippingAddress: null, shippingOption: null, trackingReference: null,
    shipments: [part('Paid', null), part('Paid', 'Mai')],
    currency: 'VND', subtotal: 1, shippingPrice: 0, taxTotal: 0, discountTotal: 0, taxRate: 0, totalAmount: 1,
    ...overrides,
  }
}

function renderAt() {
  return renderAsSeller(
    <Routes>
      <Route path="/orders/:id" element={<OrderPage />} />
    </Routes>,
    '/orders/o-1',
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('OrderPage cancelling (specs/039)', () => {
  it('cancels a waiting order only after it is confirmed', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order())
    const cancel = vi.spyOn(Order, 'cancel').mockResolvedValue(order({ status: 'Cancelled', cancelledBy: 'Customer' }))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Cancel order/ }))
    expect(await screen.findByText('Cancel this order?')).toBeInTheDocument()
    expect(cancel).not.toHaveBeenCalled()

    await user.click(screen.getAllByRole('button', { name: /Cancel order/ }).at(-1)!)
    await waitFor(() => expect(cancel).toHaveBeenCalledWith('o-1'))
  })

  it('shows the parcels of an order that is not cancelled', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order())
    renderAt()

    expect(await screen.findByRole('region', { name: 'Parcels' })).toBeInTheDocument()
  })

  it('does not offer it once a parcel is being prepared', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Preparing', shipments: [part('Preparing', 'Mai'), part('Paid', null)] }))
    renderAt()

    await screen.findByText('Ricoh GR III')
    expect(screen.queryByRole('button', { name: /Cancel order/ })).not.toBeInTheDocument()
  })

  /** A parcel started between loading the page and confirming: the server refuses, in its words. */
  it('shows the refusal in the server words', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order())
    vi.spyOn(Order, 'cancel').mockRejectedValue(refusal(409, 'This order is being prepared; ask the shop to cancel it.'))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Cancel order/ }))
    await user.click(screen.getAllByRole('button', { name: /Cancel order/ }).at(-1)!)

    expect(await screen.findByRole('alert')).toHaveTextContent('ask the shop to cancel it')
  })

  it('says who cancelled it, and shows no parcels to follow', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Cancelled', cancelledBy: 'Staff' }))
    renderAt()

    expect(await screen.findByText(/The shop cancelled this order/)).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Parcels' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Cancel order/ })).not.toBeInTheDocument()
  })

  /** specs/040: a one-parcel order has no parcel list, so "received" is offered on its own. */
  it('lets the customer say a one-parcel order arrived, after confirming', async () => {
    const shipped = { ...part('Shipped', null), id: 'p-1' }
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Shipped', shipments: [shipped] }))
    const receive = vi.spyOn(Order, 'receive').mockResolvedValue(order({ status: 'Shipped', shipments: [{ ...shipped, deliveredAt: '2026-09-24T08:00:00Z' }] }))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /received it/ }))
    expect(receive).not.toHaveBeenCalled()
    await user.click(await screen.findByRole('button', { name: /Yes, it arrived/ }))

    await waitFor(() => expect(receive).toHaveBeenCalledWith('o-1', 'p-1'))
  })

  it('offers it per parcel on a multi-parcel order, and reads delivered once all arrived', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({
      status: 'Shipped',
      shipments: [
        { ...part('Shipped', null), id: 'p-1', deliveredAt: '2026-09-24T08:00:00Z', deliveryConfirmedBy: 'Auto' },
        { ...part('Shipped', 'Mai'), id: 'p-2', deliveredAt: '2026-09-24T09:00:00Z', deliveryConfirmedBy: 'Customer' },
      ],
    }))
    renderAt()

    expect(await screen.findByText(/Delivered - you have received every parcel/)).toBeInTheDocument()
    expect(screen.getByText(/Taken as received on/)).toBeInTheDocument()
    expect(screen.getByText(/^Received on/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /received it/ })).not.toBeInTheDocument()
  })
})
