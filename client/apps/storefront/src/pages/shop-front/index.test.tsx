import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { ApiError } from '@ecommerce/core/config/axios'
import { Product } from '@ecommerce/core/services/product'
import type { Product as ProductModel } from '@ecommerce/core/services/product/types'
import { Shops } from '@ecommerce/core/services/shops'
import { Voucher } from '@ecommerce/core/services/voucher'
import { renderAsCustomer, renderAsModerator, renderSignedOut } from '@ecommerce/core/test/render'
import { ShopFrontPage } from '.'

const lens: ProductModel = {
  id: 'p1', name: 'Fujifilm X-T5', description: null, price: 41000000, currency: 'VND',
  availability: 'InStock', sku: 'FUJI-XT5', categoryId: 'c1', isActive: true, imageUrl: null,
  sellerId: 's1', sellerName: 'Mai Lens', priceVaries: false, variantCount: 1, reviewStatus: 'Approved',
  reviewReason: null, ratingAverage: null, ratingCount: 0, variants: [],
}

function renderShop(path = '/shops/s1') {
  return renderSignedOut(
    <Routes>
      <Route path="/shops/:sellerId" element={<ShopFrontPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopFrontPage', () => {
  /** What the page is for (specs/099): the shop's words, and only that shop's products. */
  it('shows the shop and asks for its products only', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: 'Used Fujifilm bodies.', productCount: 1, paused: false })
    const list = vi.spyOn(Product, 'list').mockResolvedValue({
      items: [lens], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false,
    })
    renderShop()

    expect(await screen.findByRole('heading', { name: 'Mai Lens' })).toBeInTheDocument()
    expect(screen.getByText('Used Fujifilm bodies.')).toBeInTheDocument()
    expect(screen.getByText('1 product on sale')).toBeInTheDocument()
    expect(await screen.findByText('Fujifilm X-T5')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith(expect.objectContaining({ sellerId: 's1', pageNumber: 1 }))
  })

  /** The description is the seller's text: shown as text, never as markup. */
  it('shows a description as text', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: '<b>bold</b>', productCount: 0, paused: false })
    vi.spyOn(Product, 'list').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
    renderShop()

    expect(await screen.findByText('<b>bold</b>')).toBeInTheDocument()
  })

  /** A closed or unknown shop is the server's 404 - one message, whichever it was. */
  it('says a closed shop is not open', async () => {
    vi.spyOn(Shops, 'get').mockRejectedValue(new ApiError(404, { title: 'Shop not found.' }))
    vi.spyOn(Product, 'list').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
    renderShop()

    expect(await screen.findByText('This shop is not open.')).toBeInTheDocument()
  })
})

describe('ShopFrontPage, paused and closed (specs/107)', () => {
  const empty = { items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false }
  const route = (
    <Routes>
      <Route path="/shops/:sellerId" element={<ShopFrontPage />} />
    </Routes>
  )

  /** A shopper following a link to a shop on holiday learns it is away, not that it vanished. */
  it('says a paused shop is taking a break', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: null, productCount: 0, paused: true })
    vi.spyOn(Product, 'list').mockResolvedValue(empty)
    renderShop()

    expect(await screen.findByRole('status')).toHaveTextContent('This shop is taking a break')
    expect(screen.queryByText('0 products on sale')).not.toBeInTheDocument()
  })

  /** Staff close a shop from its page, and only with the reason its seller will read. */
  it('lets staff close the shop with a reason', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: null, productCount: 0, paused: false })
    vi.spyOn(Product, 'list').mockResolvedValue(empty)
    const close = vi.spyOn(Shops, 'close').mockResolvedValue({
      sellerId: 's1', shopName: 'Mai Lens', state: 'Closed', pausedAt: null, closedAt: '2026-09-27T08:00:00Z', closedReason: 'Fakes',
    })
    vi.spyOn(Shops, 'closed').mockResolvedValue(empty)
    const user = userEvent.setup()
    renderAsModerator(route, '/shops/s1')

    await user.click(await screen.findByRole('button', { name: 'Close shop' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Close shop' })
    expect(confirm).toBeDisabled()
    await user.type(within(dialog).getByLabelText('Reason (the seller reads this)'), '  Fakes ')
    await user.click(confirm)

    await waitFor(() => expect(close).toHaveBeenCalledWith('s1', 'Fakes'))
  })

  it('offers a customer no way to close it', async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: null, productCount: 0, paused: false })
    vi.spyOn(Product, 'list').mockResolvedValue(empty)
    renderAsCustomer(route, '/shops/s1')

    expect(await screen.findByRole('heading', { name: 'Mai Lens' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Close shop' })).not.toBeInTheDocument()
  })
})

describe('ShopFrontPage vouchers (specs/114)', () => {
  it("lists the shop's public vouchers, asking for that shop only", async () => {
    vi.spyOn(Shops, 'get').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: null, productCount: 0, paused: false })
    vi.spyOn(Product, 'list').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
    const asked = vi.spyOn(Voucher, 'public').mockResolvedValue([{
      code: 'MAI10', name: 'Mai ten', isPlatform: false, sellerId: 's1', benefit: 'FixedAmount', percent: null, currency: 'VND',
      fixedValue: 50_000, maxDiscount: null, minSubtotal: null, endsAt: null, conditions: [], targeted: false,
    }])
    renderShop()

    expect(await screen.findByText('MAI10')).toBeInTheDocument()
    expect(asked).toHaveBeenCalledWith({ sellerIds: ['s1'] })
  })
})
