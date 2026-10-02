import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Order } from '@ecommerce/core/services/order'
import type { Order as OrderModel } from '@ecommerce/core/services/order/types'
import { useOrder } from '.'

let client: QueryClient

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

const order = (status: string) => ({ id: 'o1', status }) as unknown as OrderModel

/** How many times the cart was asked for again. */
function cartReads(spy: { mock: { calls: unknown[][] } }) {
  const cart = JSON.stringify(queryKeys.cart())
  return spy.mock.calls.filter(([filters]) => JSON.stringify((filters as { queryKey?: unknown } | undefined)?.queryKey) === cart).length
}

beforeEach(() => {
  client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
})

describe('useOrder (specs/119, #242)', () => {
  it('reads the cart again when the order it watched settles, and once more shortly after', async () => {
    vi.spyOn(Order, 'get').mockResolvedValueOnce(order('Submitted')).mockResolvedValue(order('Paid'))
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    const { result } = renderHook(() => useOrder('o1'), { wrapper })

    await waitFor(() => expect(result.current.data?.status).toBe('Submitted'))
    expect(cartReads(invalidate)).toBe(0)
    await waitFor(() => expect(result.current.data?.status).toBe('Paid'), { timeout: 3000 })
    await waitFor(() => expect(cartReads(invalidate)).toBe(1))
    await waitFor(() => expect(cartReads(invalidate)).toBe(2), { timeout: 3500 })
  }, 10_000)

  it('does not touch the cart for an order that was settled when the page opened', async () => {
    vi.spyOn(Order, 'get').mockResolvedValue(order('Paid'))
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    const { result } = renderHook(() => useOrder('o1'), { wrapper })

    await waitFor(() => expect(result.current.data?.status).toBe('Paid'))
    await new Promise((resolve) => setTimeout(resolve, 100))
    expect(cartReads(invalidate)).toBe(0)
  })
})
