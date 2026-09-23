import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Order } from '.'

describe('Order.sales', () => {
  /**
   * Whose sales these are comes from the token (specs/034, Constitution IV). A seller id in the query
   * string would let one seller read another's sales, and it is exactly the parameter somebody adds
   * later "for the admin view".
   */
  it('sends no seller id, ever', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Order.sales(2, 10)

    const [url, config] = get.mock.calls[0]
    expect(url).toBe('/orders/sales?page=2&pageSize=10')
    expect(url).not.toMatch(/seller|userId/i)
    // Not `anonymous: true`: this call is ABOUT the caller, so it must carry their token.
    expect(config).toBeUndefined()
  })
})

describe('Order.sale', () => {
  it('asks for one sale by the order id alone', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })

    await Order.sale('o-1')

    expect(get.mock.calls[0][0]).toBe('/orders/sales/o-1')
  })
})

describe('Order.balance and Order.payouts', () => {
  /** Whose money this is comes from the token (specs/037), exactly like the sales themselves. */
  it('asks for the caller own, naming nobody', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: [] })

    await Order.balance()
    await Order.payouts(3, 12)

    expect(get.mock.calls.map((call) => call[0])).toEqual([
      '/orders/sales/balance',
      '/orders/sales/payouts?page=3&pageSize=12',
    ])
    expect(get.mock.calls.every((call) => call[1] === undefined)).toBe(true)
  })
})

describe('Order.cancel', () => {
  /** The order is named; the owner is the token (specs/039, Constitution IV). */
  it('names the order and nobody else', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Order.cancel('o-1')

    expect(post.mock.calls[0]).toEqual(['/orders/o-1/cancel'])
  })
})

describe('Order.receive', () => {
  /** The order and the parcel are named; the owner is the token (specs/040). */
  it('names the order and the parcel, and nobody', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Order.receive('o-1', 'p-2')

    expect(post.mock.calls[0]).toEqual(['/orders/o-1/shipments/p-2/received'])
  })
})
