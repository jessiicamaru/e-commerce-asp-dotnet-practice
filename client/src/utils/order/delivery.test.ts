import { describe, expect, it } from 'vitest'
import type { Shipment } from '@/services/order/types'
import { allDelivered, canReceive } from './delivery'

const part = (status: string, deliveredAt: string | null = null): Shipment => ({
  id: 'p', status, trackingReference: null, items: [], sellerName: null, isShop: false, deliveredAt,
})

describe('delivery (specs/040)', () => {
  it('offers "received" for a shipped parcel not received yet, and nothing else', () => {
    expect(canReceive(part('Shipped'))).toBe(true)
    expect(canReceive(part('Preparing'))).toBe(false)
    expect(canReceive(part('Paid'))).toBe(false)
    expect(canReceive(part('Shipped', '2026-09-24T08:00:00Z'))).toBe(false)
  })

  it('reads an order as delivered only once every parcel is', () => {
    expect(allDelivered({ status: 'Shipped', shipments: [part('Shipped', 'x'), part('Shipped', 'y')] })).toBe(true)
    expect(allDelivered({ status: 'Shipped', shipments: [part('Shipped', 'x'), part('Shipped')] })).toBe(false)
    expect(allDelivered({ status: 'Shipped', shipments: [] })).toBe(false)
    expect(allDelivered({ status: 'Preparing', shipments: [part('Shipped', 'x')] })).toBe(false)
  })
})
