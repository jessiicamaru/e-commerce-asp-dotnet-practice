import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Order } from '@/services/order'
import type { Order as OrderModel, ParcelReturn, Shipment } from '@/services/order/types'
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

describe('OrderPage returns (specs/067)', () => {
  const daysAgo = (days: number) => new Date(Date.now() - days * 86_400_000).toISOString()
  const deliveredParcel = (deliveredAt: string, extra: Partial<Shipment> = {}): Shipment => ({
    ...part('Shipped', 'Mai'), id: 's-1', deliveredAt, deliveryConfirmedBy: 'Customer', ...extra,
  })
  const withReturn = (status: ParcelReturn['status'], extra: Partial<ParcelReturn> = {}) =>
    deliveredParcel(daysAgo(2), {
      return: {
        id: 'r-1', orderId: 'o-1', shipmentId: 's-1', isShop: false, status, reason: 'Scratched lens',
        decisionReason: null, trackingReference: null, requestedAt: daysAgo(2), decidedAt: daysAgo(1),
        sentBackAt: null, receivedAt: null, refundAmount: null, ...extra,
      },
    })

  it('asks for a reason before sending a return request for a delivered parcel', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Shipped', shipments: [deliveredParcel(daysAgo(0))] }))
    const request = vi.spyOn(Order, 'requestReturn').mockResolvedValue(withReturn('Requested').return!)
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Return this parcel/ }))
    const send = screen.getByRole('button', { name: /Ask to return it/ })
    expect(send).toBeDisabled()

    await user.type(screen.getByLabelText(/Why are you returning it/), '  Scratched lens ')
    await user.click(send)

    await waitFor(() => expect(request).toHaveBeenCalledWith('o-1', 's-1', 'Scratched lens'))
  })

  it('does not offer a return once the window has passed', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Shipped', shipments: [deliveredParcel(daysAgo(8))] }))
    renderAt()

    await screen.findByText(/^Received on/)
    expect(screen.queryByRole('button', { name: /Return this parcel/ })).not.toBeInTheDocument()
  })

  it('shows a refusal from the server in its own words', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Shipped', shipments: [deliveredParcel(daysAgo(0))] }))
    vi.spyOn(Order, 'requestReturn').mockRejectedValue(refusal(409, 'This parcel already has a return.'))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Return this parcel/ }))
    await user.type(screen.getByLabelText(/Why are you returning it/), 'Broken')
    await user.click(screen.getByRole('button', { name: /Ask to return it/ }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This parcel already has a return.')
  })

  it('sends an accepted parcel back with its tracking reference', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({ status: 'Shipped', shipments: [withReturn('Accepted')] }))
    const sendBack = vi.spyOn(Order, 'sendReturnBack').mockResolvedValue(withReturn('SentBack').return!)
    const user = userEvent.setup()
    renderAt()

    expect(await screen.findByText(/Your return was accepted. Send the parcel back by/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /sent it back/ }))
    await user.type(screen.getByLabelText('Tracking reference'), 'VNPOST-42')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sendBack).toHaveBeenCalledWith('o-1', 's-1', 'VNPOST-42'))
  })

  it("shows the seller's refusal and takes it to staff after confirming", async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({
      status: 'Shipped', shipments: [withReturn('Refused', { decisionReason: 'Used, not faulty' })],
    }))
    const escalate = vi.spyOn(Order, 'escalateReturn').mockResolvedValue(withReturn('Escalated').return!)
    const user = userEvent.setup()
    renderAt()

    expect(await screen.findByText('Refused by the seller: Used, not faulty')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /look again/ }))
    expect(escalate).not.toHaveBeenCalled()
    await user.click(await screen.findByRole('button', { name: 'Ask the shop' }))

    await waitFor(() => expect(escalate).toHaveBeenCalledWith('o-1', 's-1'))
  })

  it('offers nothing on a refusal older than the window, nor on a final rejection', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({
      status: 'Shipped',
      shipments: [
        { ...withReturn('Refused', { decisionReason: 'Too late', decidedAt: daysAgo(8) }), id: 's-1' },
        { ...withReturn('Rejected', { decisionReason: 'Worn' }), id: 's-2', isShop: true, sellerName: null },
      ],
    }))
    renderAt()

    expect(await screen.findByText(/turned the return down for good: Worn/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /look again/ })).not.toBeInTheDocument()
  })

  it('says what was refunded, in the order currency', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({
      status: 'Shipped', shipments: [withReturn('Received', { refundAmount: 3297800, receivedAt: daysAgo(0) })],
    }))
    renderAt()

    expect(await screen.findByText(/Returned. Refunded/)).toHaveTextContent(/3[.,]297[.,]800/)
  })

  it("offers each parcel's return on its own card of a multi-parcel order", async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order({
      status: 'Shipped',
      shipments: [deliveredParcel(daysAgo(1), { id: 's-1', isShop: true, sellerName: null }), deliveredParcel(daysAgo(1), { id: 's-2' })],
    }))
    const request = vi.spyOn(Order, 'requestReturn').mockResolvedValue(withReturn('Requested').return!)
    const user = userEvent.setup()
    renderAt()

    const buttons = await screen.findAllByRole('button', { name: /Return this parcel/ })
    expect(buttons).toHaveLength(2)
    await user.click(buttons[1])
    await user.type(screen.getByLabelText(/Why are you returning it/), 'Wrong lens')
    await user.click(screen.getByRole('button', { name: /Ask to return it/ }))

    await waitFor(() => expect(request).toHaveBeenCalledWith('o-1', 's-2', 'Wrong lens'))
  })
})
