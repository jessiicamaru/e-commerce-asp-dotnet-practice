import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Reviews } from '@ecommerce/core/services/review'
import type { Review } from '@ecommerce/core/services/review/types'
import { useWriteReview } from '.'

describe('useWriteReview', () => {
  /**
   * The product's average and count change with the review, and so does its shop's rating (specs/165) - the page beside
   * "Sold by" and the shop's own page must not keep showing the number from before.
   */
  it('re-reads the product, its reviews and the shops’ pages once the review is written', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>
    vi.spyOn(Reviews, 'write').mockResolvedValue({ id: 'r1' } as Review)
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    const { result } = renderHook(() => useWriteReview('p1'), { wrapper })
    result.current.mutate({ rating: 5, body: 'Sharp' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    const keys = invalidate.mock.calls.map(([filters]) => filters?.queryKey)
    expect(keys).toEqual(expect.arrayContaining([['reviews', 'p1'], ['product', 'p1'], ['shop-front']]))
    // The prefix covers every shop's page - the key the shop page and the product page share.
    expect(queryKeys.shopFront('s1').slice(0, 1)).toEqual(['shop-front'])
  })
})
