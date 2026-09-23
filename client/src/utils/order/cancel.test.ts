import { describe, expect, it } from 'vitest'
import type { Shipment } from '@/services/order/types'
import { customerCanCancel, staffCanCancel } from './cancel'

const part = (status: string): Shipment => ({ status, trackingReference: null, items: [], sellerName: null, isShop: false })

describe('who is offered cancelling (specs/039)', () => {
  it('offers a customer a paid order whose parcels all still wait', () => {
    expect(customerCanCancel({ status: 'Paid', shipments: [part('Paid'), part('Paid')] })).toBe(true)
    expect(customerCanCancel({ status: 'Paid', shipments: null })).toBe(true)
  })

  it('stops offering the customer once any parcel is being prepared', () => {
    expect(customerCanCancel({ status: 'Preparing', shipments: [part('Preparing'), part('Paid')] })).toBe(false)
  })

  it('lets staff cancel while preparing, and nobody once anything has shipped', () => {
    expect(staffCanCancel({ status: 'Preparing', shipments: [part('Preparing'), part('Paid')] })).toBe(true)
    expect(staffCanCancel({ status: 'Preparing', shipments: [part('Shipped'), part('Paid')] })).toBe(false)
    expect(customerCanCancel({ status: 'Preparing', shipments: [part('Shipped'), part('Paid')] })).toBe(false)
  })

  it('offers nothing on an order that is not paid, or already cancelled', () => {
    for (const status of ['Submitted', 'Failed', 'Cancelled', 'Shipped']) {
      expect(customerCanCancel({ status, shipments: [] })).toBe(false)
      expect(staffCanCancel({ status, shipments: [] })).toBe(false)
    }
  })
})
