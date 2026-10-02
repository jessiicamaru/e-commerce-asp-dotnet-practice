import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, renderHook, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Moderation } from '@/services/moderation'
import { ShopApplications } from '@/services/shop-applications'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { useReviewDecision } from '@/hooks/moderation'
import { AdminProductsPage } from '@/pages/admin-products'
import { renderAsModerator } from '@/test/render'
import { AdminModerationPage, RECENT_DECISIONS } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminModerationPage (specs/045)', () => {
  /** The counts are the queues' own totals, so the dashboard never disagrees with the queue it opens. */
  it('says how much is waiting in each queue and what the moderator decided', async () => {
    const products = vi.spyOn(Moderation, 'products').mockResolvedValue({
      items: [], pageNumber: 1, totalPages: 7, totalCount: 7, hasPreviousPage: false, hasNextPage: true,
    })
    vi.spyOn(ShopApplications, 'list').mockResolvedValue({ items: [], page: 1, pageSize: 1, totalCount: 2 })
    const mine = vi.spyOn(Moderation, 'myDecisions').mockResolvedValue({
      items: [{
        id: 'e1', category: 'Moderation', action: 'ProductApproved', actorId: 'm1', actorEmail: 'mod@example.test',
        actorRole: 'Moderator', subjectType: 'Product', subjectId: 'p1', summary: '"Mai Lens 35mm" approved',
        service: 'catalog', occurredAt: '2026-09-24T08:00:00Z', changeCount: 1,
      }],
      page: 1, pageSize: RECENT_DECISIONS, totalCount: 1,
    })
    renderAsModerator(
      <Routes>
        <Route path="/admin/moderation" element={<AdminModerationPage />} />
      </Routes>,
      '/admin/moderation',
    )

    expect(await screen.findByRole('link', { name: /Products waiting\s*7/ })).toHaveAttribute('href', '/admin/products')
    expect(await screen.findByRole('link', { name: /Shops waiting\s*2/ })).toHaveAttribute('href', '/admin/shops')
    expect(await screen.findByText('Product approved')).toBeInTheDocument()
    expect(products).toHaveBeenCalledWith('Pending', 1, 1)
    expect(mine).toHaveBeenCalledWith(1, RECENT_DECISIONS)
  })
})

describe('staff counts never share a list cache (specs/130, #268)', () => {
  const moderator = {
    user: { id: 'm1', email: 'mod@b.test', firstName: 'Mai', lastName: 'T', roles: ['Moderator'], emailConfirmed: true },
    restoring: false, isSeller: false, isAdmin: false, isStaff: true,
  } as unknown as AuthState
  const listed = { id: 'p1', name: 'Mai Lens 35mm', reviewStatus: 'Pending' }

  function within(client: QueryClient, path: string, children: ReactNode) {
    return (
      <AuthContext.Provider value={moderator}>
        <QueryClientProvider client={client}>
          <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
        </QueryClientProvider>
      </AuthContext.Provider>
    )
  }

  it("opens the products queue on its own page, not the dashboard's one-row count", async () => {
    vi.spyOn(Moderation, 'myDecisions').mockResolvedValue({ items: [], page: 1, pageSize: RECENT_DECISIONS, totalCount: 0 })
    vi.spyOn(ShopApplications, 'list').mockResolvedValue({ items: [], page: 1, pageSize: 1, totalCount: 0 })
    // The count reads a page of one; the queue's own page never arrives in this test.
    vi.spyOn(Moderation, 'products').mockImplementation((_status, _page, pageSize) =>
      pageSize === 1
        ? Promise.resolve({ items: [listed], pageNumber: 1, totalPages: 7, totalCount: 7, hasPreviousPage: false, hasNextPage: true } as never)
        : new Promise(() => {}),
    )
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    const dashboard = render(within(client, '/admin/moderation', <AdminModerationPage />))
    expect(await screen.findByRole('link', { name: /Products waiting\s*7/ })).toBeInTheDocument()
    dashboard.unmount()

    render(within(client, '/admin/products', <AdminProductsPage />))
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.queryByText('Mai Lens 35mm')).not.toBeInTheDocument()
  })

  it('reads the counts again after a decision', async () => {
    vi.spyOn(Moderation, 'approve').mockResolvedValue({} as never)
    const client = new QueryClient()
    const invalidate = vi.spyOn(client, 'invalidateQueries')
    const { result } = renderHook(() => useReviewDecision(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    })

    await result.current.approve.mutateAsync('p1')

    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['staff-waiting'] })
  })
})
