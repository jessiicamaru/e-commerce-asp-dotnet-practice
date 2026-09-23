import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Order } from '@/services/order'
import type { SaleSummary } from '@/services/order/types'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { renderAsSeller } from '@/test/render'
import { ShopPage } from '.'
import { noEarnings } from '@/test/fixtures'

const emptyPage = {
  items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false,
}

function listing(id: string, name: string, availability: string): ProductModel {
  return {
    id, name, description: null, price: 5190000, currency: 'VND', availability, sku: `SKU-${id}`,
    categoryId: 'c1', isActive: true, imageUrl: null, sellerId: 's1', sellerName: 'Mai Lens',
    priceVaries: false, variantCount: 1, variants: null,
  }
}

function sale(orderId: string, subtotal: number, currency: string): SaleSummary {
  return {
    orderId, status: 'Paid', createdAt: '2026-09-23T08:00:00Z', updatedAt: '2026-09-23T08:00:00Z',
    lineCount: 1, units: 1, subtotal, currency, ...noEarnings,
  }
}

beforeEach(async () => {
  // Pinned: the page would otherwise assert English on one machine and Vietnamese on another.
  await i18n.changeLanguage('en')
  vi.spyOn(Order, 'sales').mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0 })
})

describe('ShopPage (overview)', () => {
  it('shows an empty shop what to do next', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue(emptyPage)
    renderAsSeller(<ShopPage />)

    expect(await screen.findByText(/have not listed anything/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /List your first product/i })).toHaveAttribute('href', '/shop/products/new')
  })

  /**
   * Dong and dollars are never added together - the shop converts nothing (specs/022). A single
   * revenue number over both would be a sum of two different units, and look perfectly plausible.
   */
  it('shows takings per currency instead of adding dong to dollars', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue({ ...emptyPage, totalCount: 1, items: [listing('p1', 'Viltrox 56', 'InStock')] })
    vi.mocked(Order.sales).mockResolvedValue({
      items: [sale('o1', 5190000, 'VND'), sale('o2', 2380000, 'VND'), sale('o3', 54.99, 'USD')],
      page: 1, pageSize: 100, totalCount: 3,
    })
    renderAsSeller(<ShopPage />)

    const card = (await screen.findByText('Your takings')).closest('[data-slot="card"]') as HTMLElement
    expect(card.textContent).toMatch(/7[.,\s]?570[.,\s]?000/) // 5,190,000 + 2,380,000
    expect(card.textContent).toMatch(/\$54\.99/)
  })

  it('lists what has run out, linking to the listing by its id alone', async () => {
    vi.spyOn(Product, 'mine').mockResolvedValue({
      ...emptyPage,
      totalCount: 2,
      items: [listing('p1', 'Viltrox 56', 'InStock'), listing('p2', 'Sony FE 50', 'OutOfStock')],
    })
    renderAsSeller(<ShopPage />)

    const restock = (await screen.findByText('Needs restocking')).closest('[data-slot="card"]') as HTMLElement
    const link = within(restock).getByRole('link', { name: /Sony FE 50/ })
    expect(link).toHaveAttribute('href', '/shop/products/p2')
    // No address on a seller page names a seller: the token decides whose shop this is.
    expect(link.getAttribute('href')).not.toMatch(/seller|s1/i)
    expect(within(restock).queryByText('Viltrox 56')).not.toBeInTheDocument()
  })

  /** It asks for the caller's own listings and sales, and neither request can name somebody else. */
  it('reads both through endpoints that take no seller', async () => {
    const mine = vi.spyOn(Product, 'mine').mockResolvedValue(emptyPage)
    renderAsSeller(<ShopPage />)

    await screen.findByText(/have not listed anything/i)
    expect(JSON.stringify(mine.mock.calls[0][0])).not.toMatch(/seller|owner|userId/i)
  })
})
