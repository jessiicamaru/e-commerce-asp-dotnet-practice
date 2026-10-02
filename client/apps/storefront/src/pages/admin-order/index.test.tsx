import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Admin } from '@ecommerce/core/services/admin'
import type { Order, ParcelReturn, Shipment } from '@ecommerce/core/services/order/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsAdmin } from '@ecommerce/core/test/render'
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

function renderAt(id = 'o-1', from?: string) {
  return renderAsAdmin(
    <Routes>
      <Route path="/admin/orders/:id" element={<AdminOrderPage />} />
    </Routes>,
    (from ? { pathname: `/admin/orders/${id}`, state: { from } } : `/admin/orders/${id}`) as string,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminOrderPage, back where it came from (specs/129, #246)', () => {
  it('leads back to the search it was opened from, filters included', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Paid')]))
    renderAt('o-1', '/admin/orders/find?q=01a0')

    expect(await screen.findByRole('link', { name: 'Find an order' })).toHaveAttribute('href', '/admin/orders/find?q=01a0')
  })

  it('leads back to the fulfilment queue when opened directly', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Paid')]))
    renderAt()

    expect(await screen.findByRole('link', { name: 'Orders to ship' })).toHaveAttribute('href', '/admin')
  })
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

describe("AdminOrderPage the shop's own part (specs/104)", () => {
  it("cancels only the shop's part, with the reason typed, and the rest goes on", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Paid'), hers]))
    const cancel = vi.spyOn(Admin, 'cancelShopPart').mockResolvedValue(order([shop('Paid'), hers]))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Cancel this part/ }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByRole('textbox'), 'Discontinued')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel this part' }))

    await waitFor(() => expect(cancel).toHaveBeenCalledWith('o-1', 'Discontinued'))
  })

  it("says the shop's part was cancelled and why, and offers no step for it", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([{ ...shop('Cancelled'), cancelReason: 'Discontinued', cancelledBy: 'Staff' }, hers]))
    renderAt()

    expect(await screen.findByText(/The shop's part was cancelled: Discontinued/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Start preparing|Cancel this part/ })).toBeNull()
  })

  /** One part only: cancelling it IS cancelling the order, which has its own button. */
  it('offers no part cancel on an order that is the shop part alone', async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([shop('Paid')]))
    renderAt()

    await screen.findByRole('button', { name: /Start preparing/ })
    expect(screen.queryByRole('button', { name: /Cancel this part/ })).toBeNull()
  })
})

describe("AdminOrderPage correcting the shop's tracking reference (specs/105)", () => {
  it("sends the corrected reference of the shop's shipped parcel", async () => {
    vi.spyOn(Admin, 'order').mockResolvedValue(order([{ ...shop('Shipped'), trackingReference: 'SHOP-1' }, hers]))
    const correct = vi.spyOn(Admin, 'correctShopTracking').mockResolvedValue(order([{ ...shop('Shipped'), trackingReference: 'SHOP-2' }, hers]))
    const user = userEvent.setup()
    renderAt()

    await user.click(await screen.findByRole('button', { name: /Correct/ }))
    const dialog = await screen.findByRole('dialog')
    await user.clear(within(dialog).getByRole('textbox'))
    await user.type(within(dialog).getByRole('textbox'), 'SHOP-2')
    await user.click(within(dialog).getByRole('button', { name: 'Save the correction' }))

    await waitFor(() => expect(correct).toHaveBeenCalledWith('o-1', 'SHOP-2'))
  })
})
