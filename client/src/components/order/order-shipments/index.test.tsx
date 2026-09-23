import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import { OrderShipments } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('OrderShipments', () => {
  /** The point of specs/035 for a customer: "half sent" is visibly not "sent". */
  it('says how many parcels have gone, and each parcel says what is in it', () => {
    render(
      <OrderShipments
        shipments={[
          { status: 'Shipped', trackingReference: 'VN-A', items: ['Viltrox AF 56mm F1.4 · Mount: Sony E'] },
          { status: 'Preparing', trackingReference: null, items: ['SanDisk Extreme PRO · 64GB'] },
        ]}
      />,
    )

    expect(screen.getByRole('heading', { name: '1 of 2 parcels shipped' })).toBeInTheDocument()
    expect(screen.getByText('VN-A')).toBeInTheDocument()
    expect(screen.getByText('SanDisk Extreme PRO · 64GB')).toBeInTheDocument()
    expect(screen.getByText('Being prepared')).toBeInTheDocument()
  })

  /** One parcel is the order as it always was: its tracking is on the order, and there is nothing to split. */
  it('draws nothing for an order in one parcel', () => {
    const { container } = render(
      <OrderShipments shipments={[{ status: 'Shipped', trackingReference: 'VN-1', items: ['Camera'] }]} />,
    )

    expect(container).toBeEmptyDOMElement()
  })
})
