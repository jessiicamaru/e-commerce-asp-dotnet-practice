import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { OutgoingEmails } from '.'

describe('OutgoingEmails (specs/087)', () => {
  it('asks for one state, a page at a time, and leaves an empty search out', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [], page: 2, pageSize: 12, totalCount: 0 } })

    await OutgoingEmails.list('Failed', '', 2, 12)
    await OutgoingEmails.list('Sent', 'lan@', 1, 12)

    expect(get.mock.calls[0][0]).toBe('/emails?status=Failed&page=2&pageSize=12')
    expect(get.mock.calls[1][0]).toBe('/emails?status=Sent&page=1&pageSize=12&search=lan%40')
  })
})
