import { describe, expect, it, vi } from 'vitest'
import { http } from '@ecommerce/core/config/axios'
import { Order } from '@ecommerce/core/services/order'
import { Voucher } from '.'

describe('Voucher (specs/070)', () => {
  /** Whose a voucher is comes from the token - no owner in any request (Constitution IV). */
  it('creates, lists and disables through the voucher routes, naming no owner', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })
    const body = { code: 'A', name: 'A', benefit: 'Percent' as const, percent: 10, startsAt: null, endsAt: null, totalLimit: null, perCustomerLimit: null, amounts: [], conditions: [], targets: [], isPublic: false }

    await Voucher.create(body)
    await Voucher.mine(2, 12)
    await Voucher.disable('v-1')

    expect(post.mock.calls).toEqual([['/vouchers', body], ['/vouchers/v-1/disable']])
    expect(get.mock.calls[0][0]).toBe('/vouchers/mine?page=2&pageSize=12')
    expect(JSON.stringify(body)).not.toMatch(/seller|owner/i)
  })

  it('quotes with every code repeated, the way the server binds a list', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })

    await Order.quote({ addressId: 'a', shippingOption: 'standard', voucherCodes: ['SALE10', 'MAI5'] })

    expect(get.mock.calls[0][0]).toBe('/orders/quote?shippingOption=standard&addressId=a&voucherCodes=SALE10&voucherCodes=MAI5')
  })
})

describe('Voucher.public (specs/114)', () => {
  it('asks anonymously, naming the scope in the query', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: [] })

    await Voucher.public({ platform: true, sellerIds: ['s1', 's2'], productId: 'p1', variantIds: ['v1'] })

    expect(get).toHaveBeenCalledWith('/vouchers/public?platform=true&sellerId=s1&sellerId=s2&productId=p1&variantId=v1', { anonymous: true })
  })
})

describe('Voucher.mine (specs/133)', () => {
  it('sends the search and the state, and leaves out what is empty', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Voucher.mine(1, 12, { search: 'TET', state: 'Ended' })
    await Voucher.mine(1, 12, { search: '', state: '' })

    expect(get.mock.calls[0][0]).toBe('/vouchers/mine?page=1&pageSize=12&search=TET&state=Ended')
    expect(get.mock.calls[1][0]).toBe('/vouchers/mine?page=1&pageSize=12')
  })
})
