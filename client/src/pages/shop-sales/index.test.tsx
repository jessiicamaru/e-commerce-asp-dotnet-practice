import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { Order } from '@/services/order'
import type { SalePage } from '@/services/order/types'
import { ShopSalesPage } from '.'
import { noEarnings } from '@/test/fixtures'

function render_(isSeller = true) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const value = {
    user: { id: 's1', email: 'a@b.test', firstName: 'Alice', lastName: 'N', roles: isSeller ? ['Seller'] : [] },
    restoring: false,
    isSeller,
    isAdmin: false,
    isStaff: false,
    signIn: async () => {}, signUp: async () => {}, signOut: async () => {},
  } as AuthState

  return render(
    <AuthContext.Provider value={value}>
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <ShopSalesPage />
        </MemoryRouter>
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

const onePaidSale: SalePage = {
  page: 1,
  pageSize: 10,
  totalCount: 1,
  items: [{
    orderId: 'o-1',
    status: 'Paid',
    createdAt: '2026-09-23T08:14:02Z',
    updatedAt: '2026-09-23T08:14:04Z',
    lineCount: 1,
    units: 2,
    subtotal: 104000000,
    currency: 'VND',
    ...noEarnings,
  }],
}

beforeEach(async () => {
  // Pinned: the page would otherwise assert English on one machine and Vietnamese on another.
  await i18n.changeLanguage('en')
})

describe('ShopSalesPage', () => {
  it('links each sale to its own page, by order id alone', async () => {
    vi.spyOn(Order, 'sales').mockResolvedValue(onePaidSale)
    render_()

    const link = await screen.findByRole('link', { name: /Placed/ })
    expect(link).toHaveAttribute('href', '/shop/sales/o-1')
    expect(link).toHaveTextContent('1 of your lines')
    expect(link).toHaveTextContent('2 units')
    expect(link).toHaveTextContent('Paid - waiting to be prepared')
  })

  /**
   * The amount is the order's own currency, frozen at checkout (specs/022) - not whatever the seller
   * is browsing in. A dong sale read by somebody browsing in dollars must still say dong.
   */
  it('shows the subtotal in the currency the order was charged in', async () => {
    vi.spyOn(Order, 'sales').mockResolvedValue(onePaidSale)
    render_()

    const link = await screen.findByRole('link', { name: /Placed/ })
    expect(link.textContent).toMatch(/104[.,\s]?000[.,\s]?000/)
    expect(link.textContent).toMatch(/₫|VND/)
  })

  it('says why the list is empty rather than looking broken', async () => {
    vi.spyOn(Order, 'sales').mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 })
    render_()

    expect(await screen.findByText(/once it has been paid/i)).toBeInTheDocument()
  })

  /** Not a permission - the server refuses a customer by itself. It only avoids a pointless 403. */
  it('does not ask for sales when the caller is not a seller', () => {
    const sales = vi.spyOn(Order, 'sales').mockResolvedValue(onePaidSale)
    render_(false)

    expect(sales).not.toHaveBeenCalled()
  })
})
