import { describe, expect, it, vi } from 'vitest'
import { http } from '@ecommerce/core/config/axios'
import { Accounts } from '.'

describe('Accounts', () => {
  /** specs/133: a role and a state go to the server, and nothing is sent for one not chosen. */
  it('asks for people by role and state only when they are chosen', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { items: [] } })

    await Accounts.search('mai', 2, 12, false, { role: 'Seller', state: 'Locked' })
    await Accounts.search('', 1, 12)

    expect(get.mock.calls[0][0]).toBe('/users?page=2&pageSize=12&search=mai&role=Seller&state=Locked')
    expect(get.mock.calls[1][0]).toBe('/users?page=1&pageSize=12')
  })
})
