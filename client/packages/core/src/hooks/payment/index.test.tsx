import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Payment } from '@ecommerce/core/services/payment'
import type { PaymentCheckout } from '@ecommerce/core/services/payment/types'
import { usePaymentCheckout } from '.'

const preparing: PaymentCheckout = { provider: 'VnPaySandbox', state: 'Preparing', payUrl: null, expiresAt: null }
const awaiting: PaymentCheckout = { ...preparing, state: 'AwaitingPayment', payUrl: 'https://pay.test/?h=1', expiresAt: '2026-10-03T10:08:00Z' }

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('usePaymentCheckout (specs/143)', () => {
  it('asks again while Payment is still preparing, until the link arrives', async () => {
    const checkout = vi.spyOn(Payment, 'checkout').mockResolvedValueOnce(preparing).mockResolvedValue(awaiting)

    const { result } = renderHook(() => usePaymentCheckout('o-1', true), { wrapper })

    await waitFor(() => expect(result.current.data?.state).toBe('AwaitingPayment'), { timeout: 8000 })
    expect(checkout.mock.calls.length).toBeGreaterThanOrEqual(2)
  }, 10_000)

  it('asks nothing for an order that has settled', async () => {
    const checkout = vi.spyOn(Payment, 'checkout')

    renderHook(() => usePaymentCheckout('o-1', false), { wrapper })

    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(checkout).not.toHaveBeenCalled()
  })
})
