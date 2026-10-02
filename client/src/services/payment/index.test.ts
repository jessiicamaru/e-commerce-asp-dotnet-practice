import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Payment } from '.'

describe('Payment.isStub (specs/134)', () => {
  it("reads Payment's own word for its provider", async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValueOnce({ data: { provider: 'Stub - no money is moved' } })
    expect(await Payment.isStub()).toBe(true)
    expect(get.mock.calls[0][0]).toBe('/payment/health')

    get.mockResolvedValueOnce({ data: { provider: 'Acme Pay' } })
    expect(await Payment.isStub()).toBe(false)
  })

  it('knows nothing when Payment cannot be asked, or does not say', async () => {
    const get = vi.spyOn(http, 'get').mockRejectedValueOnce(new Error('unreachable'))
    expect(await Payment.isStub()).toBeNull()

    get.mockResolvedValueOnce({ data: {} })
    expect(await Payment.isStub()).toBeNull()
  })
})
