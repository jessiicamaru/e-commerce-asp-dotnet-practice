import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { Product } from '@/services/product'
import type { Product as ProductModel, Variant } from '@/services/product/types'
import { ProductPage } from '.'

function aVariant(over: Partial<Variant>): Variant {
  return {
    id: 'v1', sku: 'SKU-1', price: 41000000, currency: 'VND',
    optionSummary: 'Màu: Đen', options: [{ id: 'o1', name: 'Màu', value: 'Đen' }],
    availability: 'InStock', isActive: true, imageUrl: null, ...over,
  }
}

function aProduct(variants: Variant[]): ProductModel {
  return {
    id: 'p1', name: 'Fujifilm X-T5', description: null, price: 41000000, currency: 'VND',
    availability: 'InStock', sku: 'FUJI-XT5', categoryId: 'c1', isActive: true,
    imageUrl: '/api/products/p1/image?v=1', sellerId: null, sellerName: null,
    priceVaries: false, variantCount: variants.length, variants,
  }
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const auth = {
    user: null, restoring: false, isSeller: false, isAdmin: false,
    signIn: async () => {}, signUp: async () => {}, signOut: async () => {},
  } as AuthState

  return render(
    <AuthContext.Provider value={auth}>
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={['/products/p1']}>
          <Routes>
            <Route path="/products/:id" element={<ProductPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ProductPage pictures', () => {
  /** The whole point of specs/032: the picture follows the choice. */
  it('shows the chosen variant’s own photograph', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(aProduct([
      aVariant({ id: 'black', sku: 'XT5-BLACK', optionSummary: 'Colour: Black',
                 options: [{ id: 'o1', name: 'Colour', value: 'Black' }],
                 imageUrl: '/api/products/p1/variants/black/image?v=9' }),
      aVariant({ id: 'silver', sku: 'XT5-SILVER', optionSummary: 'Colour: Silver',
                 options: [{ id: 'o2', name: 'Colour', value: 'Silver' }],
                 imageUrl: '/api/products/p1/variants/silver/image?v=7' }),
    ]))
    const user = userEvent.setup()
    renderPage()

    await waitFor(() => expect(screen.getByRole('img')).toHaveAttribute('src', '/api/products/p1/image?v=1'))

    await user.click(screen.getByRole('radio', { name: /Silver/i }))

    await waitFor(() =>
      expect(screen.getByRole('img')).toHaveAttribute('src', '/api/products/p1/variants/silver/image?v=7'))
  })

  /**
   * The server folds the fallback into variant.imageUrl, so a variant with no photograph of its
   * own arrives carrying the PRODUCT's address. The page must render what it is given rather than
   * deciding again - deciding again is how one of the two places gets it wrong, and the wrong one
   * keeps showing the previous variant.
   */
  it('shows the product’s photograph for a variant that has none of its own', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(aProduct([
      aVariant({ id: 'black', sku: 'XT5-BLACK', optionSummary: 'Colour: Black',
                 options: [{ id: 'o1', name: 'Colour', value: 'Black' }],
                 imageUrl: '/api/products/p1/variants/black/image?v=9' }),
      aVariant({ id: 'silver', sku: 'XT5-SILVER', optionSummary: 'Colour: Silver',
                 options: [{ id: 'o2', name: 'Colour', value: 'Silver' }],
                 imageUrl: '/api/products/p1/image?v=1' }),
    ]))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('radio', { name: /Silver/i }))

    await waitFor(() =>
      expect(screen.getByRole('img')).toHaveAttribute('src', '/api/products/p1/image?v=1'))
  })

  /** A product sold one way has nothing to choose, and that variant's picture is simply it. */
  it('uses the single variant’s photograph with no choosing', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(aProduct([
      aVariant({ id: 'only', optionSummary: '', options: [],
                 imageUrl: '/api/products/p1/variants/only/image?v=5' }),
    ]))
    renderPage()

    await waitFor(() =>
      expect(screen.getByRole('img')).toHaveAttribute('src', '/api/products/p1/variants/only/image?v=5'))
  })
})
