import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Product } from '@/services/product'
import { useMyProducts } from '.'

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('useMyProducts', () => {
  /**
   * `/products/mine` is [Authorize(Roles = "Seller")]. A customer asking for it collects a 403 on
   * every page load - a refusal in the logs that means nothing is wrong, which is the kind of noise
   * that later gets investigated. `enabled` is what stops it.
   */
  it('does not ask at all when the person is not a seller', async () => {
    const mine = vi.spyOn(Product, 'mine').mockResolvedValue({
      items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false,
    })

    renderHook(() => useMyProducts({ pageNumber: 1 }, false), { wrapper })

    await new Promise((resolve) => setTimeout(resolve, 20))
    expect(mine).not.toHaveBeenCalled()
  })

  it('asks once when the person is a seller', async () => {
    const mine = vi.spyOn(Product, 'mine').mockResolvedValue({
      items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false,
    })

    const { result } = renderHook(() => useMyProducts({ pageNumber: 1 }, true), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mine).toHaveBeenCalledWith({ pageNumber: 1 })
  })
})
