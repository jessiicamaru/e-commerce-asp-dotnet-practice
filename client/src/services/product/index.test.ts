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

describe('Product.recordView (specs/086)', () => {
  /** One visitor is one view a day: the same id every time this browser opens a page, whichever product. */
  it('names this browser the same way every time', async () => {
    localStorage.clear()
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: undefined })

    await Product.recordView('p1')
    await Product.recordView('p2')

    const [[url, first], [, second]] = post.mock.calls as unknown as [string, { viewer?: string }][]
    expect(url).toBe('/products/p1/view')
    expect(first.viewer).toMatch(/^[0-9a-f-]{36}$/)
    expect(second.viewer).toBe(first.viewer)
  })

  it('keeps a visitor id already made, and replaces one that is not an id', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: undefined })
    localStorage.setItem('visitor-id', '4f1c2d3e-0000-4000-8000-000000000001')
    await Product.recordView('p1')
    localStorage.setItem('visitor-id', 'not-an-id')
    await Product.recordView('p1')

    const bodies = post.mock.calls.map(([, body]) => (body as { viewer?: string }).viewer)
    expect(bodies[0]).toBe('4f1c2d3e-0000-4000-8000-000000000001')
    expect(bodies[1]).not.toBe('not-an-id')
    expect(localStorage.getItem('visitor-id')).toBe(bodies[1])
  })

  /** Without storage a fresh id per call would dedupe nothing - send none, and let the gateway limit. */
  it('sends no visitor id when the browser keeps nothing', async () => {
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: undefined })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    await Product.recordView('p1')

    expect((post.mock.calls[0][1] as { viewer?: string }).viewer).toBeUndefined()
  })
})
