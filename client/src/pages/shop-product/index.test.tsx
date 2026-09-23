import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Product } from '@/services/product'
import { Stock } from '@/services/stock'
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
  reviewStatus: 'Approved',
  reviewReason: null,
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
      imageUrl: null,
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
          <Route path="/shop/products" element={<p>listings</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

/** What each currency's own response looks like: one currency's prices, never both (specs/022). */
function inCurrency(currency: string, price: number | null, variantImage: string | null = null): ProductModel {
  return {
    ...listing,
    price,
    currency,
    variants: [{ ...listing.variants[0], price, currency, imageUrl: variantImage }],
  }
}

function refusal(detail: string) {
  return import('axios').then(({ AxiosError }) => {
    const error = new AxiosError('failed')
    error.response = {
      status: 404,
      data: { status: 404, detail },
      statusText: '', headers: {}, config: { headers: {} as never },
    }
    return error
  })
}

const priceBox = (currency: string) => screen.findByLabelText(`Price in ${currency}`)

beforeEach(async () => {
  await i18n.changeLanguage('en')
  localStorage.setItem('currency', 'USD')
  vi.spyOn(Product, 'get').mockImplementation(async (_id, currency) =>
    currency === 'USD' ? inCurrency('USD', null) : inCurrency('VND', 52000000),
  )
  vi.spyOn(Stock, 'get').mockResolvedValue({
    productId: 'p1', sku: 'SONY-A7M4', quantityOnHand: 3, quantityReserved: 2, quantityAvailable: 1,
  })
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

    expect(await priceBox('VND')).toBeInTheDocument()
    expect(await priceBox('USD')).toBeInTheDocument()
  })

  /** Nothing converts (specs/022): the amount goes to the currency whose box it was typed into. */
  it('saves a price against the currency it was typed under', async () => {
    const setPrice = vi.spyOn(Product, 'setPrice').mockResolvedValue()
    const user = userEvent.setup()
    renderPage()

    await user.type(await priceBox('USD'), '1999')
    await user.click(screen.getByRole('button', { name: /Save variant/i }))

    await waitFor(() => expect(setPrice).toHaveBeenCalledWith('p1', 'p1', 'USD', 1999))
    // Only what changed is sent. Re-sending the untouched VND price could overwrite somebody else's edit.
    expect(setPrice).toHaveBeenCalledTimes(1)
  })

  /** Nothing to save, nothing to press: a save button that sends an unchanged form is a no-op at best. */
  it('does not offer to save until something has changed', async () => {
    renderPage()

    await priceBox('VND')
    expect(screen.getByRole('button', { name: /Save variant/i })).toBeDisabled()
  })

  /** Withdrawal is permanent (specs/024). A misclick must not be enough. */
  it('does not withdraw without a confirmation', async () => {
    const remove = vi.spyOn(Product, 'remove').mockResolvedValue()
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Withdraw this listing/i }))
    await user.click(await screen.findByRole('button', { name: /Cancel/i }))

    expect(remove).not.toHaveBeenCalled()
  })

  it('withdraws once it is confirmed', async () => {
    const remove = vi.spyOn(Product, 'remove').mockResolvedValue()
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Withdraw this listing/i }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(
      [...dialog.querySelectorAll('button')].find((button) => /Withdraw this listing/i.test(button.textContent ?? ''))!,
    )

    await waitFor(() => expect(remove).toHaveBeenCalledWith('p1'))
  })

  /**
   * SellerOwnership answers 404 for somebody else's listing, deliberately indistinguishable from a
   * product that does not exist. The page must repeat the server's words rather than translate them
   * into "you are not allowed" - which would undo the reason it is a 404.
   */
  it('does not turn the server 404 into a permission message', async () => {
    vi.spyOn(Product, 'setPrice').mockRejectedValue(await refusal("Product with ID 'p1' was not found."))
    const user = userEvent.setup()
    renderPage()

    fireEvent.change(await priceBox('VND'), { target: { value: '51000000' } })
    await user.click(screen.getByRole('button', { name: /Save variant/i }))

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('was not found'))
    expect(screen.queryByText(/not allowed|forbidden|permission/i)).not.toBeInTheDocument()
  })
})

describe('SellerProductPage prices, read per currency', () => {
  /**
   * The bug this test exists for, seen in a screenshot: browsing in USD, the VND box was EMPTY
   * although a VND price existed. A response carries one currency's prices, so reading the product
   * once and filtering by `variant.currency` can only ever fill in the currency already being
   * browsed in - and this is the one page whose whole job is the OTHER one.
   */
  it('shows the price of a currency the seller is not browsing in', async () => {
    renderPage()

    // Grouped for reading - and read back as the same number, which pendingChanges' tests pin.
    await waitFor(async () => expect(await priceBox('VND')).toHaveValue('52 000 000'))
    expect(await priceBox('USD')).toHaveValue('')
  })

  it('asks once per currency, with the currency stated rather than inherited', async () => {
    const get = vi.mocked(Product.get)
    renderPage()

    await waitFor(() => expect(get.mock.calls.length).toBeGreaterThanOrEqual(2))
    expect([...new Set(get.mock.calls.map((call) => call[1]))].sort()).toEqual(['USD', 'VND'])
  })

  /** Null is not zero: a currency nobody priced says so, instead of offering the camera for nothing. */
  it('says a currency has no price rather than showing 0', async () => {
    renderPage()

    const usd = await priceBox('USD')
    await waitFor(() => expect(usd).toHaveAttribute('placeholder', 'No price in this currency'))
    expect(usd).not.toHaveValue('0')
  })
})

describe('SellerProductPage stock', () => {
  /** A variant id, never a product id: Inventory keys stock by the sellable unit (specs/020). */
  it('sets stock against the variant, with the number as a number', async () => {
    const setOnHand = vi.spyOn(Stock, 'setOnHand').mockResolvedValue({
      productId: 'p1', sku: 'SONY-A7M4', quantityOnHand: 9, quantityReserved: 2, quantityAvailable: 7,
    })
    const user = userEvent.setup()
    renderPage()

    const box = await screen.findByLabelText(/On hand/i)
    await waitFor(() => expect(box).toHaveValue('3'))
    // fireEvent.change, not user.clear + type: clear() leaves this controlled input at its
    // prefilled "3" and typing appends, so the test would assert 39 and pass for the wrong reason.
    fireEvent.change(box, { target: { value: '9' } })
    await user.click(screen.getByRole('button', { name: /Save variant/i }))

    await waitFor(() => expect(setOnHand).toHaveBeenCalledWith('p1', 9))
    expect(typeof setOnHand.mock.calls[0][1]).toBe('number')
  })

  /**
   * "I have 3 but only 1 is available" reads as a bug until you know two are inside somebody's
   * checkout. Reserved is shown for exactly that reason.
   */
  it('says how many are held for orders in progress', async () => {
    renderPage()

    expect(await screen.findByText(/2 held in checkouts/i)).toBeInTheDocument()
  })

  /** It shows what is on hand, not what is available - those differ whenever a sale is mid-flight. */
  it('prefills with on hand, not with available', async () => {
    renderPage()

    await waitFor(async () => expect(await screen.findByLabelText(/On hand/i)).toHaveValue('3'))
  })

  /**
   * A freshly listed product has no stock row for a moment - it is created off the broker. That is
   * a real state and must not be drawn as a zero, which would be a lie.
   */
  it('says when the stock row has not arrived yet, instead of showing zero', async () => {
    vi.spyOn(Stock, 'get').mockRejectedValue(await refusal("Product 'p1' is not registered in inventory."))
    renderPage()

    expect(await screen.findByText(/still being registered with the warehouse/i)).toBeInTheDocument()
    const box = screen.getByLabelText(/On hand/i)
    expect(box).toHaveValue('')
    expect(box).toBeDisabled()
    expect(screen.queryByText(/^0$/)).not.toBeInTheDocument()
  })

  /** The server's 404 for somebody else's product stays a 404 here - never "you are not allowed". */
  it('repeats the server refusal instead of interpreting it', async () => {
    vi.spyOn(Stock, 'setOnHand').mockRejectedValue(await refusal("Product with ID 'p1' was not found."))
    const user = userEvent.setup()
    renderPage()

    const box = await screen.findByLabelText(/On hand/i)
    await waitFor(() => expect(box).toHaveValue('3'))
    fireEvent.change(box, { target: { value: '5' } })
    await user.click(screen.getByRole('button', { name: /Save variant/i }))

    await waitFor(() =>
      expect(screen.getAllByRole('alert').some((a) => /was not found/.test(a.textContent ?? ''))).toBe(true),
    )
    expect(screen.queryByText(/not allowed|forbidden|permission/i)).not.toBeInTheDocument()
  })
})

describe('SellerProductPage variant photograph', () => {
  const png = () => new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'x.png', { type: 'image/png' })

  it('uploads a dropped photograph against the variant', async () => {
    const upload = vi.spyOn(Product, 'uploadVariantImage').mockResolvedValue()
    renderPage()

    fireEvent.drop(await screen.findByRole('button', { name: /Photograph of SONY-A7M4/i }), {
      dataTransfer: { files: [png()] },
    })

    await waitFor(() => expect(upload).toHaveBeenCalled())
    const [productId, variantId, file] = upload.mock.calls[0]
    expect(productId).toBe('p1')
    expect(variantId).toBe('p1') // the first variant reuses the product id (specs/020)
    expect(file).toBeInstanceOf(File)
  })

  /** Removing is not "no picture": the shape goes back to showing the product's. */
  it('offers to fall back to the product photograph when the variant has one of its own', async () => {
    vi.mocked(Product.get).mockImplementation(async (_id, currency) =>
      inCurrency(currency ?? 'VND', currency === 'USD' ? null : 52000000, '/api/products/p1/variants/p1/image?v=1'),
    )
    const remove = vi.spyOn(Product, 'removeVariantImage').mockResolvedValue()
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Use the product.s photograph/i }))

    await waitFor(() => expect(remove).toHaveBeenCalledWith('p1', 'p1'))
  })

  /** Nothing of its own to remove, nothing to offer: the button used to sit there doing nothing. */
  it('does not offer the fall-back when the variant already shows the product photograph', async () => {
    renderPage()

    await priceBox('VND')
    expect(screen.queryByRole('button', { name: /Use the product.s photograph/i })).not.toBeInTheDocument()
  })
})
