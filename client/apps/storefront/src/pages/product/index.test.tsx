import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState } from '@ecommerce/core/context/auth/types'
import { Product } from '@ecommerce/core/services/product'
import { Reviews } from '@ecommerce/core/services/review'
import type { Product as ProductModel, Variant } from '@ecommerce/core/services/product/types'
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
    priceVaries: false, variantCount: variants.length, reviewStatus: 'Approved', reviewReason: null, ratingAverage: null, ratingCount: 0, variants,
  }
}

/** Where Add to cart sent a signed-out shopper, and what it told the sign-in page. */
function SignInStub() {
  const location = useLocation()
  return <p data-testid="sign-in">{JSON.stringify(location.state)}</p>
}

function renderPage(path = '/products/p1') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const auth = {
    user: null, restoring: false, isSeller: false, isAdmin: false, isStaff: false, refreshSession: async () => true,
    signIn: async () => ({ setupRequired: false }), completeSignIn: async () => {}, signUp: async () => {}, signOut: async () => {},
  } as AuthState

  return render(
    <AuthContext.Provider value={auth}>
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/products/:id" element={<ProductPage />} />
            <Route path="/sign-in" element={<SignInStub />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  // The reviews under the product (specs/046) - none, so these tests stay about the product itself.
  vi.spyOn(Product, 'recordView').mockResolvedValue()
  vi.spyOn(Reviews, 'forProduct').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
})

describe('ProductPage, signed out (specs/126, #252)', () => {
  const kits = () =>
    aProduct([
      aVariant({ id: 'body', sku: 'XT5-BODY', optionSummary: 'Kit: Body only', options: [{ id: 'o1', name: 'Kit', value: 'Body only' }] }),
      aVariant({ id: 'kit', sku: 'XT5-KIT', optionSummary: 'Kit: With lens', options: [{ id: 'o2', name: 'Kit', value: 'With lens' }] }),
    ])

  it('offers Add to cart, and sends the shopper to sign in with their choice and why', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(kits())
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('radio', { name: /With lens/ }))
    await user.click(screen.getByRole('button', { name: 'Add to cart' }))

    expect(JSON.parse(screen.getByTestId('sign-in').textContent!)).toEqual({ from: '/products/p1?variant=kit', reason: 'cart' })
  })

  it('comes back with the variant chosen before signing in', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(kits())
    renderPage('/products/p1?variant=kit')

    expect(await screen.findByRole('radio', { name: /With lens/ })).toBeChecked()
    expect(screen.getByText('SKU XT5-KIT')).toBeInTheDocument()
  })

  /** specs/020 D10 is kept: with several shapes nothing is chosen - and then no SKU names one. */
  it('names no SKU until a variant is chosen', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(kits())
    const user = userEvent.setup()
    renderPage()

    await screen.findByRole('radio', { name: /Body only/ })
    expect(screen.queryByText(/^SKU /)).not.toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: /Body only/ }))
    expect(screen.getByText('SKU XT5-BODY')).toBeInTheDocument()
  })
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

describe('ProductPage views', () => {
  /** What people look at (specs/047): one view per product opened, not one per render or per choice. */
  it('reports the page once, however often it renders', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(aProduct([
      aVariant({ id: 'black', sku: 'XT5-BLACK', optionSummary: 'Colour: Black', options: [{ id: 'o1', name: 'Colour', value: 'Black' }] }),
      aVariant({ id: 'silver', sku: 'XT5-SILVER', optionSummary: 'Colour: Silver', options: [{ id: 'o2', name: 'Colour', value: 'Silver' }] }),
    ]))
    const view = vi.mocked(Product.recordView)
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('radio', { name: /Silver/i }))
    await user.click(screen.getByRole('radio', { name: /Black/i }))

    expect(view).toHaveBeenCalledTimes(1)
    expect(view).toHaveBeenCalledWith('p1')
  })
})

describe('ProductPage seller', () => {
  /** A shop has a page (specs/099): the credit under the name goes to it. */
  it('links a seller’s name to their shop’s page', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue({ ...aProduct([aVariant({})]), sellerId: 's1', sellerName: 'Mai Lens' })
    renderPage()

    expect(await screen.findByRole('link', { name: 'Mai Lens' })).toHaveAttribute('href', '/shops/s1')
  })

  /** The shop's own goods have no seller, so no page to go to - the credit is text. */
  it('credits the shop itself without a link', async () => {
    vi.spyOn(Product, 'get').mockResolvedValue(aProduct([aVariant({})]))
    renderPage()

    expect(await screen.findByText(/Sold by/)).toBeInTheDocument()
    expect(screen.getAllByRole('link').filter((link) => link.getAttribute('href')?.startsWith('/shops/'))).toEqual([])
  })
})
