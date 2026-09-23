import { describe, expect, it } from 'vitest'
import type { Order, Shipment } from '@/services/order/types'
import { shopParcelOf } from './shop-parcel'

function order(shipments: Shipment[] | null, overrides: Partial<Order> = {}): Order {
  return {
    orderId: 'o-1', status: 'Preparing', failureReason: null, createdAt: '', updatedAt: '',
    items: [{
      productId: 'p', productName: 'Ricoh GR III', variantId: null, sku: null, optionSummary: 'Colour: Black',
      quantity: 1, unitPrice: 1, totalPrice: 1, taxAmount: null,
    }],
    shippingAddress: null, shippingOption: null, trackingReference: null, shipments,
    currency: 'VND', subtotal: 1, shippingPrice: 0, taxTotal: 0, discountTotal: 0, taxRate: 0, totalAmount: 1,
    ...overrides,
  }
}

const parcel = (isShop: boolean, status: string): Shipment => ({
  status, trackingReference: null, items: [isShop ? 'Strap' : 'Lens'], sellerName: isShop ? null : 'Mai', isShop,
})

describe('shopParcelOf', () => {
  it('is the shop part of an order, in THAT part state - not the order state', () => {
    expect(shopParcelOf(order([parcel(false, 'Shipped'), parcel(true, 'Paid')]))).toEqual({
      status: 'Paid', trackingReference: null, items: ['Strap'],
    })
  })

  /** Every parcel is a seller's: staff have nothing to move, and must not be offered a button. */
  it('is nothing when the order holds none of the shop goods', () => {
    expect(shopParcelOf(order([parcel(false, 'Paid')]))).toBeNull()
  })

  /** An order an older image wrote has no parts; then the whole order is the shop's, in its own state. */
  it('is the whole order when the order has no parts at all', () => {
    expect(shopParcelOf(order(null, { trackingReference: 'VN-9' }))).toEqual({
      status: 'Preparing', trackingReference: 'VN-9', items: ['Ricoh GR III · Colour: Black'],
    })
  })
})
