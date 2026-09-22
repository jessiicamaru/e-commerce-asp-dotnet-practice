import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { SellerProductPage } from '.'

const listing = {
  id: 'p1',
  name: 'Sony A7 IV',
  description: null,
  price: 52000000,
  currency: 'VND',
  availability: 'OutOfStock',
  sku: 'SONY-A7M4',
  categoryId: 'c1',
  isActive: true,
  imageUrl: null,
  sellerId: 's1',
  sellerName: 'Alice Cameras',
  priceVaries: false,
  variantCount: 1,
  variants: [
    {
      id: 'p1',
      sku: 'SONY-A7M4',
      price: 52000000,
      currency: 'VND',
      optionSummary: '',
      options: [],
      availability: 'OutOfStock',
      isActive: true,
    },
  ],
} satisfies ProductModel

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/shop/products/p1']}>
        <Routes>
          <Route path="/shop/products/:id" element={<SellerProductPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Product, 'get').mockResolvedValue(listing)
})

describe('SellerProductPage', () => {
  /**
   * The create form can only set one currency (research D2), so this page is where the second one
   * gets filled in. Showing only the active currency would hide the very gap a seller came to close,
   * and a product priced in one currency and not the other is the most confusing state specs/022
   * can produce.
   */
  it('offers every currency the shop prices in, not just the one being browsed in', async () => {
    renderPage()

    await waitFor(() => expect(screen.getByLabelText('VND SONY-A7M4')).toBeInTheDocument())
    expect(screen.getByLabelText('USD SONY-A7M4')).toBeInTheDocument()
  })

  /** Nothing converts (specs/022): the amount goes to the currency whose row it was typed into. */
  it('saves a price against the currency it was typed under', async () => {
    const setPrice = vi.spyOn(Product, 'setPrice').mockResolvedValue()
    const user = userEvent.setup()
    renderPage()

    await waitFor(() => expect(screen.getByLabelText('USD SONY-A7M4')).toBeInTheDocument())
    await user.type(screen.getByLabelText('USD SONY-A7M4'), '1999')
    await user.click(screen.getAllByRole('button', { name: /Save price/i })[1])

    await waitFor(() => expect(setPrice).toHaveBeenCalled())
    expect(setPrice).toHaveBeenCalledWith('p1', 'p1', 'USD', 1999)
  })

  /** Withdrawal is permanent (specs/024). A misclick must not be enough. */
  it('does not withdraw without a confirmation', async () => {
    const remove = vi.spyOn(Product, 'remove').mockResolvedValue()
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const user = userEvent.setup()
    renderPage()

    await waitFor(() => expect(screen.getByRole('button', { name: /Withdraw/i })).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /Withdraw/i }))

    expect(remove).not.toHaveBeenCalled()
  })

  it('withdraws once it is confirmed', async () => {
    const remove = vi.spyOn(Product, 'remove').mockResolvedValue()
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const user = userEvent.setup()
    renderPage()

    await waitFor(() => expect(screen.getByRole('button', { name: /Withdraw/i })).toBeInTheDocument())
    await user.click(screen.getByRole('button', { name: /Withdraw/i }))

    await waitFor(() => expect(remove).toHaveBeenCalledWith('p1'))
  })

  /**
   * SellerOwnership answers 404 for somebody else's listing, deliberately indistinguishable from a
   * product that does not exist. The page must repeat the server's words rather than translate them
   * into "you are not allowed" - which would undo the reason it is a 404.
   */
  it('does not turn the server 404 into a permission message', async () => {
    const { AxiosError } = await import('axios')
    const error = new AxiosError('failed')
    error.response = {
      status: 404,
      data: { status: 404, detail: "Product with ID 'p1' was not found." },
      statusText: '', headers: {}, config: { headers: {} as never },
    }
    vi.spyOn(Product, 'setPrice').mockRejectedValue(error)
    const user = userEvent.setup()
    renderPage()

    await waitFor(() => expect(screen.getByLabelText('VND SONY-A7M4')).toBeInTheDocument())
    await user.click(screen.getAllByRole('button', { name: /Save price/i })[0])

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('was not found'))
    expect(screen.queryByText(/not allowed|forbidden|permission/i)).not.toBeInTheDocument()
  })
})
