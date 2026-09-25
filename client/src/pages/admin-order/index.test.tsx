import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Admin } from '@/services/admin'
import type { Order, ParcelReturn, Shipment } from '@/services/order/types'
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

  /** specs/039: staff cancel, confirmed, through the staff route. */
  it('cancels the order for the customer, while a parcel is being prepared', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Preparing'), hers]))
    const cancel = vi.spyOn(Admin, 'cancel').mockResolvedValue({ ...order([shop('Preparing'), hers]), status: 'Cancelled', cancelledBy: 'Staff' })
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Cancel order/ }))
    await user.click(screen.getAllByRole('button', { name: /Cancel order/ }).at(-1)!)

    await waitFor(() => expect(cancel).toHaveBeenCalledWith('o-1'))
  })

  it('offers no step and no cancel on a cancelled order, and says who cancelled it', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue({ ...order([shop('Paid'), hers]), status: 'Cancelled', cancelledBy: 'Customer' })
    renderAt()

    expect(await screen.findByText(/The customer cancelled this order/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Start preparing/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Cancel order/ })).not.toBeInTheDocument()
  })

  it('shows a refusal in the server words', async () => {
    vi.spyOn(Admin, 'order').mockRejectedValue(refusal(404, 'Order not found.'))
    renderAt('missing')

    expect(await screen.findByRole('alert')).toHaveTextContent('Order not found.')
  })
})

describe('AdminOrderPage returns (specs/067)', () => {
  const ret = (status: ParcelReturn['status'], isShop: boolean, extra: Partial<ParcelReturn> = {}): ParcelReturn => ({
    id: `r-${isShop}`, orderId: 'o-1', shipmentId: isShop ? 's-shop' : 's-mai', isShop, status, reason: 'Scratched lens',
    decisionReason: null, trackingReference: null, requestedAt: '2026-09-24T08:00:00Z', decidedAt: null,
    sentBackAt: null, receivedAt: null, refundAmount: null, ...extra,
  })
  const shopWith = (r: ParcelReturn): Shipment => ({ ...shop('Shipped'), id: 's-shop', deliveredAt: '2026-09-23T08:00:00Z', return: r })
  const hersWith = (r: ParcelReturn): Shipment => ({ ...hers, status: 'Shipped', id: 's-mai', deliveredAt: '2026-09-23T08:00:00Z', return: r })
  const shippedOrder = (shipments: Shipment[]) => ({ ...order(shipments), status: 'Shipped' })

  it("accepts a return of the shop's own parcel through the staff route, for that parcel", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(shippedOrder([shopWith(ret('Requested', true)), hers]))
    const accept = vi.spyOn(Admin, 'acceptReturn').mockResolvedValue(ret('Accepted', true))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Accept the return/ }))
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: /Accept the return/ }))

    await waitFor(() => expect(accept).toHaveBeenCalledWith('o-1', 's-shop'))
  })

  it("receives the shop's own parcel back", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(shippedOrder([shopWith(ret('SentBack', true, { trackingReference: 'VN-9' }))]))
    const receive = vi.spyOn(Admin, 'receiveReturn').mockResolvedValue(ret('Received', true))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Mark as received/ }))
    await user.click(await screen.findByRole('button', { name: /Yes, it came back/ }))

    await waitFor(() => expect(receive).toHaveBeenCalledWith('o-1', 's-shop'))
  })

  /** An escalated refusal is staff's final word - worded as such, sent for THAT parcel. */
  it("rejects an escalated seller's return for good, with a reason", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(shippedOrder([shop('Shipped'), hersWith(ret('Escalated', false, { decisionReason: 'Used' }))]))
    const refuse = vi.spyOn(Admin, 'refuseReturn').mockResolvedValue(ret('Rejected', false))
    const user = userEvent.setup()
    renderAt()

    expect(await screen.findByText('Parcel from Mai')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Reject for good/ }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/This is the final word/)).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('Reason'), 'Worn, not faulty')
    await user.click(within(dialog).getByRole('button', { name: /Reject for good/ }))

    await waitFor(() => expect(refuse).toHaveBeenCalledWith('o-1', 's-mai', 'Worn, not faulty'))
  })

  it("draws a seller's return that is not escalated with nothing to press", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(shippedOrder([shop('Shipped'), hersWith(ret('Requested', false))]))
    renderAt()

    expect(await screen.findByText(/The seller answers this one/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Accept the return/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Refuse/ })).not.toBeInTheDocument()
  })
})
