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
