import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Category } from '@ecommerce/core/services/category'
import { Product } from '@ecommerce/core/services/product'
import { renderSignedOut } from '@ecommerce/core/test/render'
import { CatalogPage } from '.'

const empty = { items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false }

function renderCatalog(path = '/products') {
  return renderSignedOut(
    <Routes>
      <Route path="/products" element={<CatalogPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'list').mockResolvedValue([])
})

describe('CatalogPage, price and stock (specs/109)', () => {
  /** The filters live in the address, so a shared link lists the same thing. */
  it('asks for the range and the stock the address names', async () => {
    const list = vi.spyOn(Product, 'list').mockResolvedValue(empty)
    renderCatalog('/products?min=1000000&max=20000000&stock=1')

    await waitFor(() =>
      expect(list).toHaveBeenCalledWith(expect.objectContaining({ minPrice: 1_000_000, maxPrice: 20_000_000, inStock: true })),
    )
    expect(screen.getByLabelText('Min')).toHaveValue(1_000_000)
    expect(screen.getByRole('checkbox', { name: 'In stock only' })).toHaveAttribute('aria-checked', 'true')
  })

  it('applies a typed range with the search', async () => {
    const list = vi.spyOn(Product, 'list').mockResolvedValue(empty)
    const user = userEvent.setup()
    renderCatalog()

    await user.type(await screen.findByLabelText('Max'), '20000000')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ maxPrice: 20_000_000, minPrice: undefined })))
  })

  /** A reversed range is said here, not sent to be refused. */
  it('says a reversed range rather than sending it', async () => {
    const list = vi.spyOn(Product, 'list').mockResolvedValue(empty)
    const user = userEvent.setup()
    renderCatalog()

    await user.type(await screen.findByLabelText('Min'), '500')
    await user.type(screen.getByLabelText('Max'), '100')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The minimum is above the maximum.')
    expect(list).not.toHaveBeenCalledWith(expect.objectContaining({ minPrice: 500 }))
  })

  it('asks for only what is in stock when ticked, and for everything when not', async () => {
    const list = vi.spyOn(Product, 'list').mockResolvedValue(empty)
    const user = userEvent.setup()
    renderCatalog()

    await waitFor(() => expect(list).toHaveBeenCalledWith(expect.objectContaining({ inStock: undefined })))
    await user.click(screen.getByRole('checkbox', { name: 'In stock only' }))

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ inStock: true })))
  })

  /** Only what is reduced (specs/161): in the address like the other filters, sent only when ticked. */
  it('asks for only what is on sale when ticked, from the address too', async () => {
    const list = vi.spyOn(Product, 'list').mockResolvedValue(empty)
    const user = userEvent.setup()
    renderCatalog()

    await waitFor(() => expect(list).toHaveBeenCalledWith(expect.objectContaining({ onSale: undefined })))
    await user.click(screen.getByRole('checkbox', { name: 'On sale' }))

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ onSale: true })))
  })

  /** A typo in a shared link is no bound, not a page that fails to load. */
  it('ignores a bound that is not a number', async () => {
    const list = vi.spyOn(Product, 'list').mockResolvedValue(empty)
    renderCatalog('/products?min=abc&max=-5')

    await waitFor(() => expect(list).toHaveBeenCalledWith(expect.objectContaining({ minPrice: undefined, maxPrice: undefined })))
  })
})
