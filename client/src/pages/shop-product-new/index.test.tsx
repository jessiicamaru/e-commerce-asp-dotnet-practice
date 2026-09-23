import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Category } from '@/services/category'
import { Product } from '@/services/product'
import { NewProductPage } from '.'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <NewProductPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(async () => {
  // Pinned, not inherited. i18next's detector reads navigator.language, so without this the suite
  // would assert English on one machine and Vietnamese on another - a flake that only appears in CI.
  await i18n.changeLanguage('en')

  vi.spyOn(Category, 'list').mockResolvedValue([
    { id: 'cat-1', name: 'Mirrorless cameras', description: null, slug: 'mirrorless', parentCategoryId: null, isActive: true },
  ])
})

async function fillAndSubmit() {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText(/Name/i), 'Sony A7 IV')
  // A combobox now, the way a person uses it: type part of the name, pick the match.
  await user.type(screen.getByLabelText(/Category/i), 'mirror')
  await user.click(await screen.findByRole('option', { name: 'Mirrorless cameras' }))
  await user.type(screen.getByLabelText(/^SKU$/i), 'SONY-A7M4')
  await user.type(screen.getByLabelText(/Price in/i), '52000000')
  await user.click(screen.getByRole('button', { name: /List it/i }))
}

describe('NewProductPage', () => {
  /**
   * The defect this test exists for: the server stores this number as the DEFAULT currency's
   * amount (specs/022) and validates it against that currency. A field labelled with the currency
   * the seller happens to be browsing in would let somebody reading in dollars type 1999 and list a
   * 1,999-dong camera, with no refusal anywhere - the price fits VND perfectly well.
   */
  it('labels the price with the default currency, not the one being browsed in', async () => {
    localStorage.setItem('currency', 'USD')
    renderPage()

    expect(screen.getByLabelText(/Price in VND/i)).toBeInTheDocument()
    expect(screen.queryByLabelText(/Price in USD/i)).not.toBeInTheDocument()
    localStorage.removeItem('currency')
  })

  it('sends exactly what was typed, and no seller', async () => {
    const create = vi.spyOn(Product, 'create').mockResolvedValue({ id: 'p1' } as never)
    renderPage()

    await fillAndSubmit()

    await waitFor(() => expect(create).toHaveBeenCalled())
    const sent = create.mock.calls[0][0]
    expect(sent).toEqual({
      name: 'Sony A7 IV',
      description: null,
      sku: 'SONY-A7M4',
      price: 52000000,
      categoryId: 'cat-1',
    })
    // No field may name an owner: the server takes it from the token (specs/027).
    expect(Object.keys(sent).join()).not.toMatch(/seller|owner|userId/i)
  })

  /** A price is a number to the server. Sending "52000000" as a string is a 400 nobody expected. */
  it('sends the price as a number', async () => {
    const create = vi.spyOn(Product, 'create').mockResolvedValue({ id: 'p1' } as never)
    renderPage()

    await fillAndSubmit()

    await waitFor(() => expect(create).toHaveBeenCalled())
    expect(typeof create.mock.calls[0][0].price).toBe('number')
  })

  it('shows the server refusal in the server words', async () => {
    const { AxiosError } = await import('axios')
    const error = new AxiosError('failed')
    error.response = {
      status: 409,
      data: { status: 409, detail: "A variant with SKU 'SONY-A7M4' already exists." },
      statusText: '', headers: {}, config: { headers: {} as never },
    }
    vi.spyOn(Product, 'create').mockRejectedValue(error)
    renderPage()

    await fillAndSubmit()

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent("A variant with SKU 'SONY-A7M4' already exists."),
    )
  })

  /** Stated up front, because a seller whose listing reads "out of stock" will otherwise think it broke. */
  it('says the three things that surprise a new seller', () => {
    renderPage()

    expect(screen.getByText(/no stock/i)).toBeInTheDocument()
    expect(screen.getByText(/other currency/i)).toBeInTheDocument()
    expect(screen.getByText(/other language/i)).toBeInTheDocument()
  })
})

describe('NewProductPage in Vietnamese', () => {
  it('is translated, and still labels the price in the default currency', async () => {
    await i18n.changeLanguage('vi')
    renderPage()

    expect(screen.getByRole('heading', { name: 'Đăng sản phẩm' })).toBeInTheDocument()
    expect(screen.getByLabelText(/Giá theo VND/i)).toBeInTheDocument()
    expect(screen.getByText(/chưa có tồn kho/i)).toBeInTheDocument()

    await i18n.changeLanguage('en')
  })
})
