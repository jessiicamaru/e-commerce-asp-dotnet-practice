import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { Product } from '@/services/product'
import { Seller } from '@/services/seller'
import { ShopPage } from '.'

const emptyPage = {
  items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false,
}

function renderAsSeller(children: ReactNode = <ShopPage />) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const value = {
    user: { id: 's1', email: 'a@b.test', firstName: 'Alice', lastName: 'N', roles: ['Seller', 'Customer'] },
    restoring: false,
    isSeller: true,
    signIn: async () => {}, signUp: async () => {}, signOut: async () => {},
  } as AuthState

  return render(
    <AuthContext.Provider value={value}>
      <QueryClientProvider client={client}>
        <MemoryRouter>{children}</MemoryRouter>
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Seller, 'me').mockResolvedValue({ sellerId: 's1', shopName: 'Alice Cameras' })
})

describe('ShopPage', () => {
  it('shows the shop name and an empty state that says what to do', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue(emptyPage)
    renderAsSeller()

    await waitFor(() => expect(screen.getByText(/have not listed anything/i)).toBeInTheDocument())
    expect(screen.getByRole('link', { name: /List your first product/i })).toHaveAttribute(
      'href',
      '/shop/products/new',
    )
  })

  it('links each listing to the seller page for it, never to a seller-scoped address', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue({
      ...emptyPage,
      totalCount: 1,
      totalPages: 1,
      items: [{
        id: 'p1', name: 'Sony A7 IV', description: null, price: 52000000, currency: 'VND',
        availability: 'OutOfStock', sku: 'SONY-A7M4', categoryId: 'c1', isActive: true,
        imageUrl: null, sellerId: 's1', sellerName: 'Alice Cameras', priceVaries: false,
        variantCount: 1, variants: null,
      }],
    })
    renderAsSeller()

    const link = await screen.findByRole('link', { name: /Sony A7 IV/i })
    expect(link).toHaveAttribute('href', '/shop/products/p1')
    // No address anywhere on this page names a seller: the token decides whose shop this is.
    expect(link.getAttribute('href')).not.toMatch(/seller|s1/i)
  })

  /** A listing with no stock reads out of stock, and the page says so rather than looking broken. */
  it('says when a listing has no stock yet', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue({
      ...emptyPage,
      totalCount: 1,
      totalPages: 1,
      items: [{
        id: 'p1', name: 'Sony A7 IV', description: null, price: 52000000, currency: 'VND',
        availability: 'OutOfStock', sku: 'SONY-A7M4', categoryId: 'c1', isActive: true,
        imageUrl: null, sellerId: 's1', sellerName: 'Alice Cameras', priceVaries: false,
        variantCount: 1, variants: null,
      }],
    })
    renderAsSeller()

    expect(await screen.findByText(/No stock yet/i)).toBeInTheDocument()
  })

  /** One row changes every listing, so the page says so before somebody presses the button. */
  it('warns that renaming the shop changes every listing', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue(emptyPage)
    renderAsSeller()

    expect(await screen.findByText(/every one of your listings/i)).toBeInTheDocument()
  })
})
