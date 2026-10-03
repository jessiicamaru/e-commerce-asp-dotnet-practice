import { describe, expect, it, vi } from 'vitest'
import { http } from '@ecommerce/core/config/axios'
import { Payment } from '.'

describe('Payment.about (specs/134, specs/143)', () => {
  it("reads Payment's own word for whether money moves and where the customer pays", async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValueOnce({
      data: { provider: 'VnPay sandbox - no money is moved', movesMoney: false, configuredOutcome: 'Customer' },
    })
    expect(await Payment.about()).toEqual({ movesMoney: false, atGateway: true })
    expect(get.mock.calls[0][0]).toBe('/payment/health')

    get.mockResolvedValueOnce({ data: { provider: 'VnPay', movesMoney: true, configuredOutcome: 'Customer' } })
    expect(await Payment.about()).toEqual({ movesMoney: true, atGateway: true })
  })

  it("falls back to the stub's name from before movesMoney existed", async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValueOnce({ data: { provider: 'Stub - no money is moved', configuredOutcome: 'Approve' } })
    expect(await Payment.about()).toEqual({ movesMoney: false, atGateway: false })

    get.mockResolvedValueOnce({ data: { provider: 'Acme Pay' } })
    expect(await Payment.about()).toEqual({ movesMoney: true, atGateway: false })
  })

  it('knows nothing when Payment cannot be asked, or does not say', async () => {
    const get = vi.spyOn(http, 'get').mockRejectedValueOnce(new Error('unreachable'))
    expect(await Payment.about()).toEqual({ movesMoney: null, atGateway: false })

    get.mockResolvedValueOnce({ data: {} })
    expect(await Payment.about()).toEqual({ movesMoney: null, atGateway: false })
  })
})

describe('Payment.checkout (specs/143)', () => {
  it("asks Payment about one order's payment", async () => {
    const answer = { provider: 'VnPaySandbox', state: 'AwaitingPayment', payUrl: 'https://pay.test/?x', expiresAt: '2026-10-03T10:08:00Z' }
    const get = vi.spyOn(http, 'get').mockResolvedValueOnce({ data: answer })
    expect(await Payment.checkout('o-1')).toEqual(answer)
    expect(get).toHaveBeenCalledWith('/payments/orders/o-1/checkout')
  })
})
