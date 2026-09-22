import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Product } from '.'

describe('Product.mine', () => {
  /**
   * The point of `/products/mine` is that it takes no seller id: whose shop it is comes from the
   * token (specs/027). An id in the query string would let one seller read another's page, and it
   * is the sort of parameter somebody adds later "for the admin view". This test is what refuses
   * it.
   */
  it('sends no seller id, ever', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Product.mine({ pageNumber: 1, pageSize: 20 })

    const [url] = get.mock.calls[0]
    expect(url).not.toMatch(/seller/i)
    expect(url).not.toMatch(/userId/i)
  })

  /** Not `anonymous: true`: this call is ABOUT the caller, so it must carry their token. */
  it('is an authenticated call', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Product.mine({ pageNumber: 1 })

    const [, config] = get.mock.calls[0]
    expect(config).toBeUndefined()
  })

  it('drops empty values instead of sending blank filters', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Product.mine({ pageNumber: 2, searchTerm: '' })

    const [url] = get.mock.calls[0]
    expect(url).toContain('pageNumber=2')
    expect(url).not.toContain('searchTerm')
  })
})
