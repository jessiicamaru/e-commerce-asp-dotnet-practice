import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Admin } from '.'

describe('Admin', () => {
  it('reads a queue by state, page and page size', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Admin.fulfilment('Preparing', 2, 12)

    expect(get.mock.calls[0][0]).toBe('/orders/fulfilment?status=Preparing&page=2&pageSize=12')
  })

  /** The staff read, never the owner-scoped `/orders/{id}` - that one answers 404 for staff. */
  it('reads an order through the staff route', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })

    await Admin.order('o-1')

    expect(get.mock.calls[0][0]).toBe('/orders/fulfilment/o-1')
  })

  it('moves the shop parcel with the staff endpoints', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Admin.prepare('o-1')
    await Admin.ship('o-1', 'VNPOST-1')

    expect(post.mock.calls[0]).toEqual(['/orders/o-1/preparing'])
    expect(post.mock.calls[1]).toEqual(['/orders/o-1/shipment', { trackingReference: 'VNPOST-1' }])
  })

  /**
   * specs/037: a payout names a seller and a currency and NO amount - the server pays what the parcels
   * add up to. An amount here would be a second opinion the ledger could disagree with.
   */
  it('records a payout with no amount in it', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Admin.pay('s-1', 'VND')

    expect(post.mock.calls[0]).toEqual(['/orders/payouts', { sellerId: 's-1', currency: 'VND' }])
  })

  it('cancels through the staff route', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Admin.cancel('o-1')

    expect(post.mock.calls[0]).toEqual(['/orders/fulfilment/o-1/cancel'])
  })
})

describe('Admin returns (specs/067)', () => {
  it('reads the queue by state and moves one parcel through the fulfilment routes', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Admin.returns('Escalated', 2, 12)
    await Admin.acceptReturn('o-1', 's-2')
    await Admin.refuseReturn('o-1', 's-2', 'Worn')
    await Admin.receiveReturn('o-1', 's-2')

    expect(get.mock.calls[0][0]).toBe('/orders/returns?status=Escalated&page=2&pageSize=12')
    expect(post.mock.calls).toEqual([
      ['/orders/fulfilment/o-1/shipments/s-2/return/accept'],
      ['/orders/fulfilment/o-1/shipments/s-2/return/refuse', { reason: 'Worn' }],
      ['/orders/fulfilment/o-1/shipments/s-2/return/received'],
    ])
  })
})
