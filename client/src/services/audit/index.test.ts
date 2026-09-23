import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Audit } from '.'

describe('Audit', () => {
  it('asks for a page with only the filters that were chosen', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Audit.list({ category: 'Payment', actor: 'mai@', action: undefined }, 2, 12)

    const url = new URL(`http://x${get.mock.calls[0][0]}`)
    expect(url.pathname).toBe('/audit')
    expect(Object.fromEntries(url.searchParams)).toEqual({ page: '2', pageSize: '12', category: 'Payment', actor: 'mai@' })
  })

  it('reads one entry and the summary', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: [] })

    await Audit.get('e-1')
    await Audit.summary('2026-09-17T12:00:00.000Z')

    expect(get.mock.calls.map((c) => c[0])).toEqual(['/audit/e-1', '/audit/summary?from=2026-09-17T12%3A00%3A00.000Z'])
  })
})
