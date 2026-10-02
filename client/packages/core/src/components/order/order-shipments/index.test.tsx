import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render as renderBare, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { OrderShipments } from '.'

// A tracking reference asks who the carrier is (specs/098), which needs a query client.
const render = (ui: ReactNode) =>
  renderBare(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>)

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('OrderShipments', () => {
  /** The point of specs/035 for a customer: "half sent" is visibly not "sent". */
  it('says how many parcels have gone, and each parcel says what is in it', () => {
    render(
      <OrderShipments
        shipments={[
          { status: 'Shipped', trackingReference: 'VN-A', items: ['Viltrox AF 56mm F1.4 · Mount: Sony E'], sellerName: 'Mai Lens', isShop: false },
          { status: 'Preparing', trackingReference: null, items: ['SanDisk Extreme PRO · 64GB'], sellerName: 'Saigon Accessories', isShop: false },
        ]}
      />,
    )

    expect(screen.getByRole('heading', { name: '1 of 2 parcels shipped' })).toBeInTheDocument()
    expect(screen.getByText('VN-A')).toBeInTheDocument()
    expect(screen.getByText('SanDisk Extreme PRO · 64GB')).toBeInTheDocument()
    expect(screen.getByText('Being prepared')).toBeInTheDocument()
  })

  /** specs/036: who sends each parcel. The shop's own is worded in the reader's language. */
  it('names the shop each parcel comes from, and the shop itself as "the shop"', () => {
    render(
      <OrderShipments
        shipments={[
          { status: 'Paid', trackingReference: null, items: ['Ricoh GR IIIx'], sellerName: null, isShop: true },
          { status: 'Shipped', trackingReference: 'GHN-1', items: ['SanDisk 256GB'], sellerName: 'Saigon Accessories', isShop: false },
        ]}
      />,
    )

    expect(screen.getByText('from the shop')).toBeInTheDocument()
    expect(screen.getByText('from Saigon Accessories')).toBeInTheDocument()
  })

  /** A seller's parcel with no recorded name (an older order) shows no line, never "from null". */
  it('says nothing about the sender when no name was recorded', () => {
    render(
      <OrderShipments
        shipments={[
          { status: 'Paid', trackingReference: null, items: ['A'], sellerName: null, isShop: false },
          { status: 'Paid', trackingReference: null, items: ['B'], sellerName: null, isShop: false },
        ]}
      />,
    )

    expect(screen.queryByText(/^from /)).not.toBeInTheDocument()
    expect(screen.queryByText(/null|undefined/)).not.toBeInTheDocument()
  })

  /** One parcel is the order as it always was: its tracking is on the order, and there is nothing to split. */
  it('draws nothing for an order in one parcel', () => {
    const { container } = render(
      <OrderShipments
        shipments={[{ status: 'Shipped', trackingReference: 'VN-1', items: ['Camera'], sellerName: null, isShop: true }]}
      />,
    )

    expect(container).toBeEmptyDOMElement()
  })
})

describe('OrderShipments a cancelled part (specs/104)', () => {
  it('says which part was cancelled and why, and does not count it as a parcel on its way', () => {
    render(
      <OrderShipments
        shipments={[
          { status: 'Shipped', trackingReference: 'VN-A', items: ['Viltrox 56mm'], sellerName: 'Mai Lens', isShop: false },
          {
            status: 'Cancelled', trackingReference: null, items: ['SanDisk 64GB'], sellerName: 'Saigon Accessories', isShop: false,
            cancelledAt: '2026-09-27T09:00:00Z', cancelReason: 'Out of stock', cancelledBy: 'Seller',
          },
        ]}
      />,
    )

    expect(screen.getByRole('heading', { name: '1 of 1 parcels shipped' })).toBeInTheDocument()
    expect(screen.getByText('Cancelled')).toBeInTheDocument()
    expect(screen.getByText(/Cancelled: Out of stock\. You are refunded what you paid for it\./)).toBeInTheDocument()
  })
})
