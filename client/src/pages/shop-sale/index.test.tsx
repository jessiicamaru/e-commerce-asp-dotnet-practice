import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError } from 'axios'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Order } from '@/services/order'
import type { ParcelReturn, Sale } from '@/services/order/types'
import { ShopSalePage } from '.'
import { noEarnings } from '@/test/fixtures'

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

const address = {
  recipientName: 'Lan Pham', line1: '12 Ly Thuong Kiet', line2: null, city: 'Ha Noi', region: null,
  postalCode: '100000', country: 'VN', phone: '+84 912 345 678',
}

function sale(status: string, overrides: Partial<Sale> = {}): Sale {
  return {
    orderId: 'o-1',
    status,
    createdAt: '2026-09-23T08:14:02Z',
    updatedAt: '2026-09-23T09:30:11Z',
    currency: 'USD',
    language: 'en',
    subtotal: 2798,
    trackingReference: null,
    shippingAddress: status === 'Shipped' ? null : address,
    ...noEarnings,
    items: [{
      productId: 'p1', productName: 'Sony A7 IV', variantId: 'v1', sku: 'SONY-A7M4',
      optionSummary: 'Kit: Body only', quantity: 2, unitPrice: 1399, totalPrice: 2798, taxAmount: 279.8,
    }],
    ...overrides,
  }
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopSalePage', () => {
  it('asks for the sale named in the address, and shows the seller its lines', async () => {
    const get = vi.spyOn(Order, 'sale').mockResolvedValue(sale('Paid'))
    renderAt('o-1')

    expect(await screen.findByText('Sony A7 IV')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('o-1')
    expect(screen.getByText('Kit: Body only')).toBeInTheDocument()
  })

  it('labels the subtotal as the seller part, in the order currency', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Paid'))
    renderAt('o-1')

    // The subtotal line, not the line total beside it - both say $2,798.00 on a one-line sale.
    const subtotal = (await screen.findByText(/Before tax, your goods only/)).previousElementSibling
    expect(subtotal).toHaveTextContent('$2,798.00')
    // Said on the page, because a subtotal next to "your lines" otherwise reads as what they are paid.
    expect(screen.getByText(/commission and your share of delivery are under/)).toBeInTheDocument()
  })

  /** specs/037: what the sale earns them sits beside the lines, in the order's own currency. */
  it('shows what the sale earns the seller', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Shipped', {
      goodsTotal: 2798, commission: 279.8, shippingShare: 2.5, payout: 2520.7, paidOut: false,
      deliveredAt: '2026-09-24T08:00:00Z',
    }))
    renderAt('o-1')

    const earnings = (await screen.findByText('You receive', { selector: '[data-slot="card-title"]' })).closest('[data-slot="card"]') as HTMLElement
    expect(within(earnings).getByText('$279.80')).toBeInTheDocument()
    expect(within(earnings).getByText('$2,520.70')).toBeInTheDocument()
    expect(within(earnings).getByText('Due')).toBeInTheDocument()
    // specs/040: due BECAUSE it arrived - and the page says so.
    expect(screen.getByText(/The customer received it on/)).toBeInTheDocument()
  })

  it('keeps a shipped parcel on the way until the customer has it', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Shipped', {
      goodsTotal: 2798, commission: 279.8, shippingShare: 2.5, payout: 2520.7, paidOut: false, deliveredAt: null,
    }))
    renderAt('o-1')

    const earnings = (await screen.findByText('You receive', { selector: '[data-slot="card-title"]' })).closest('[data-slot="card"]') as HTMLElement
    expect(within(earnings).getByText('On the way')).toBeInTheDocument()
    expect(screen.queryByText(/The customer received it/)).not.toBeInTheDocument()
  })

  /** specs/039: the seller learns to stop - no step to take, no address. */
  it('shows a cancelled sale with nothing to do', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Cancelled', { shippingAddress: null }))
    renderAt('o-1')

    expect(await screen.findByText(/This order was cancelled/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Start preparing/ })).not.toBeInTheDocument()
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

describe('ShopSalePage shipping the seller part', () => {
  /** Only the next step is offered - the same forwards-one-at-a-time rule the server enforces. */
  it('offers to start preparing a waiting part, for this order', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Paid'))
    const prepare = vi.spyOn(Order, 'prepareSale').mockResolvedValue(sale('Preparing'))
    const user = userEvent.setup()
    renderAt('o-1')

    await user.click(await screen.findByRole('button', { name: /Start preparing/i }))

    await waitFor(() => expect(prepare).toHaveBeenCalledWith('o-1'))
    expect(screen.queryByRole('button', { name: /Mark as shipped/i })).not.toBeInTheDocument()
  })

  /** The tracking reference is asked for in a dialog: it is what the customer follows, and it is final. */
  it('ships a part being prepared with the tracking reference typed in the dialog', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Preparing'))
    const ship = vi.spyOn(Order, 'shipSale').mockResolvedValue(sale('Shipped', { trackingReference: 'VN-1' }))
    const user = userEvent.setup()
    renderAt('o-1')

    await user.click(await screen.findByRole('button', { name: /Mark as shipped/i }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Tracking reference'), '  VN-1 ')
    await user.click(within(dialog).getByRole('button', { name: /Mark as shipped/i }))

    await waitFor(() => expect(ship).toHaveBeenCalledWith('o-1', 'VN-1'))
  })

  it('shows where to send it while the part is not yet sent', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Preparing'))
    renderAt('o-1')

    expect(await screen.findByText('Lan Pham')).toBeInTheDocument()
    expect(screen.getByText('+84 912 345 678')).toBeInTheDocument()
  })

  /** research D6: once the parcel is out the server stops sending the address, and the page says why. */
  it('says the address is gone once the part is shipped, and offers no further step', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Shipped', { trackingReference: 'VN-9' }))
    renderAt('o-1')

    expect(await screen.findByText(/no longer shown/i)).toBeInTheDocument()
    expect(screen.queryByText('Lan Pham')).not.toBeInTheDocument()
    expect(screen.getByText('VN-9')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Start preparing|Mark as shipped/i })).not.toBeInTheDocument()
  })
})

describe('ShopSalePage returns (specs/067)', () => {
  const ret = (status: ParcelReturn['status'], extra: Partial<ParcelReturn> = {}): ParcelReturn => ({
    id: 'r-1', orderId: 'o-1', shipmentId: 's-1', isShop: false, status, reason: 'Scratched lens',
    decisionReason: null, trackingReference: null, requestedAt: '2026-09-24T08:00:00Z', decidedAt: null,
    sentBackAt: null, receivedAt: null, refundAmount: null, ...extra,
  })
  const withReturn = (r: ParcelReturn) => sale('Shipped', { deliveredAt: '2026-09-23T08:00:00Z', return: r })

  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })

  it("shows the buyer's reason and accepts the request after confirming", async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(withReturn(ret('Requested')))
    const accept = vi.spyOn(Order, 'acceptSaleReturn').mockResolvedValue(ret('Accepted'))
    const user = userEvent.setup()
    renderAt('o-1')

    expect(await screen.findByText('Scratched lens')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Accept the return/ }))
    expect(accept).not.toHaveBeenCalled()
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: /Accept the return/ }))

    await waitFor(() => expect(accept).toHaveBeenCalledWith('o-1'))
  })

  it('refuses only with a reason the buyer can read', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(withReturn(ret('Requested')))
    const refuse = vi.spyOn(Order, 'refuseSaleReturn').mockResolvedValue(ret('Refused'))
    const user = userEvent.setup()
    renderAt('o-1')

    await user.click(await screen.findByRole('button', { name: /Refuse/ }))
    const dialog = await screen.findByRole('dialog')
    const send = within(dialog).getByRole('button', { name: 'Refuse' })
    expect(send).toBeDisabled()

    await user.type(within(dialog).getByLabelText('Reason'), 'Used, not faulty')
    await user.click(send)

    await waitFor(() => expect(refuse).toHaveBeenCalledWith('o-1', 'Used, not faulty'))
  })

  it('marks a parcel sent back as received, after saying it refunds the buyer', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(withReturn(ret('SentBack', { trackingReference: 'VN-9', sentBackAt: '2026-09-25T08:00:00Z' })))
    const receive = vi.spyOn(Order, 'receiveSaleReturn').mockResolvedValue(ret('Received'))
    const user = userEvent.setup()
    renderAt('o-1')

    expect(await screen.findByText(/\(VN-9\)/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Accept the return/ })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Mark as received/ }))
    expect(await screen.findByText(/refunded the goods and their tax/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Yes, it came back/ }))

    await waitFor(() => expect(receive).toHaveBeenCalledWith('o-1'))
  })

  /** Escalated is staff's to decide; the seller only reads where it has got to. */
  it('offers the seller nothing on an escalated return', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(withReturn(ret('Escalated', { decisionReason: 'Used' })))
    renderAt('o-1')

    expect(await screen.findByText(/took the refusal to the shop/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Accept the return/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Refuse/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Mark as received/ })).not.toBeInTheDocument()
  })

  it('shows no return card on a sale without one', async () => {
    vi.spyOn(Order, 'sale').mockResolvedValue(sale('Shipped'))
    renderAt('o-1')

    await screen.findByText('Sony A7 IV')
    expect(screen.queryByText("The buyer’s reason")).not.toBeInTheDocument()
  })
})
