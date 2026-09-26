import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { SavedProduct } from '.'

describe('SavedProduct (specs/075)', () => {
  /** Whose list it is comes from the token: no shopper id anywhere (Constitution IV). */
  it('saves, unsaves and reads the caller own list, naming no shopper', async () => {
    const put = vi.spyOn(http, 'put').mockResolvedValue({ data: undefined })
    const del = vi.spyOn(http, 'delete').mockResolvedValue({ data: undefined })
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: [] })

    await SavedProduct.save('p1')
    await SavedProduct.unsave('p1')
    await SavedProduct.list(2, 12)
    await SavedProduct.ids()

    expect(put.mock.calls[0][0]).toBe('/products/p1/saved')
    expect(del.mock.calls[0][0]).toBe('/products/p1/saved')
    expect(get.mock.calls.map((c) => c[0])).toEqual(['/products/saved?page=2&pageSize=12', '/products/saved/ids'])
  })
})
