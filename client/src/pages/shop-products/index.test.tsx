import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { renderAsSeller } from '@/test/render'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { Stock } from '@/services/stock'
import { ShopProductsPage } from '.'

function withVariants(id: string, name: string, variantIds: string[]): ProductModel {
  return {
    id, name, description: null, price: 15490000, currency: 'VND', availability: 'InStock', sku: `SKU-${id}`,
    categoryId: 'c1', isActive: true, imageUrl: null, sellerId: 's1', sellerName: 'Mai Lens',
    priceVaries: true, variantCount: variantIds.length, reviewStatus: 'Approved', reviewReason: null,
    variants: variantIds.map((variantId) => ({
      id: variantId, sku: `SKU-${variantId}`, price: 15490000, currency: 'VND', optionSummary: '', options: [],
      availability: 'InStock', isActive: true, imageUrl: null,
    })),
  }
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopProductsPage', () => {
  /**
   * The number a seller reorders by is Inventory's, summed over the listing's variants - not
   * Catalog's "in stock" flag, which is a read model and never a count (specs/004).
   */
  it('shows how many are left, summed over every variant', async () => {
    const sigma = withVariants('p1', 'Sigma 18-50', ['v1', 'v2'])
    vi.spyOn(Product, 'mine').mockResolvedValue({
      items: [{ ...sigma, variants: null }], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false,
    })
    vi.spyOn(Product, 'get').mockResolvedValue(sigma)
    vi.spyOn(Stock, 'get').mockImplementation(async (variantId) => ({
      productId: variantId, sku: variantId, quantityOnHand: variantId === 'v1' ? 6 : 4, quantityReserved: 0,
      quantityAvailable: variantId === 'v1' ? 6 : 4,
    }))
    renderAsSeller(<ShopProductsPage />)

    const row = (await screen.findByRole('link', { name: 'Sigma 18-50' })).closest('tr') as HTMLElement
    expect(await within(row).findByText('10 in stock')).toBeInTheDocument()
    expect(within(row).getByText('2 variants')).toBeInTheDocument()
  })

  /** A variant whose stock row has not arrived yet is "not known", never 0 - that would read as sold out. */
  it('says stock is not known yet rather than calling a new listing sold out', async () => {
    const fresh = withVariants('p2', 'Just listed', ['p2'])
    vi.spyOn(Product, 'mine').mockResolvedValue({
      items: [{ ...fresh, variants: null }], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false,
    })
    vi.spyOn(Product, 'get').mockResolvedValue(fresh)
    const { AxiosError } = await import('axios')
    const missing = new AxiosError('failed')
    missing.response = { status: 404, data: {}, statusText: '', headers: {}, config: { headers: {} as never } }
    vi.spyOn(Stock, 'get').mockRejectedValue(missing)
    renderAsSeller(<ShopProductsPage />)

    expect(await screen.findByText('Stock not known yet')).toBeInTheDocument()
    expect(screen.queryByText('Out of stock')).not.toBeInTheDocument()
  })

  it('asks for its own page size and names no seller', async () => {
    const mine = vi.spyOn(Product, 'mine').mockResolvedValue({
      items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false,
    })
    renderAsSeller(<ShopProductsPage />)

    await screen.findByText(/A product you list here/i)
    expect(mine.mock.calls[0][0]).toMatchObject({ pageNumber: 1, pageSize: 12 })
    expect(JSON.stringify(mine.mock.calls[0][0])).not.toMatch(/seller|owner|userId/i)
  })
})
