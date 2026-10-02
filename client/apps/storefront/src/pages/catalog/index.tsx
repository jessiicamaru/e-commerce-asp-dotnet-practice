import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { CatalogFilters } from '@/components/catalog/catalog-filters'
import { CatalogHero } from '@/components/catalog/catalog-hero'
import { ProductCard } from '@/components/product/product-card'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { Pager } from '@/components/shared/pager'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { useCategories } from '@ecommerce/core/hooks/category'
import { useProducts } from '@ecommerce/core/hooks/product'
import type { SortBy } from '@ecommerce/core/services/product/types'

/** The listing (#36), driven by the URL so a search can be shared, bookmarked and survives a reload. */
export function CatalogPage() {
  const { t } = useTranslation('catalog')
  const [params, setParams] = useSearchParams()
  const searchTerm = params.get('q') ?? ''
  const categoryId = params.get('category') ?? ''
  const sortBy = (params.get('sort') as SortBy | null) ?? 'name_asc'
  const pageNumber = Number(params.get('page') ?? '1') || 1
  // A price range in the currency being browsed in, and "in stock only" (specs/109) - in the URL like the rest.
  const minPrice = params.get('min') ?? ''
  const maxPrice = params.get('max') ?? ''
  const inStock = params.get('stock') === '1'

  const categories = useCategories()
  const products = useProducts({
    pageNumber,
    pageSize: PAGE_SIZE,
    searchTerm,
    categoryId,
    sortBy,
    minPrice: bound(minPrice),
    maxPrice: bound(maxPrice),
    inStock: inStock || undefined,
  })

  // The hero belongs to the landing view only. Once somebody has searched or filtered, the results
  // are what they came for and a hero is in the way of them.
  const landing = !searchTerm && !categoryId && !minPrice && !maxPrice && !inStock && pageNumber === 1

  function update(changes: Record<string, string>) {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value) {
        next.set(key, value)
      } else {
        next.delete(key)
      }
    }
    if (!('page' in changes)) {
      next.delete('page') // a new filter starts on page 1
    }
    setParams(next)
  }

  const categoryName = (id: string) => categories.data?.find((category) => category.id === id)?.name
  const page = products.data

  return (
    <>
      {landing && page && (
        <CatalogHero
          productCount={page.totalCount}
          categories={categories.data ?? []}
          // The dearest one, not the first: featuring items[0] put the same card in the hero and
          // immediately again as the first tile under it, which looking at the page made obvious.
          featured={[...page.items].sort((a, b) => (b.price ?? 0) - (a.price ?? 0))[0]}
          onCategory={(id) => update({ category: id })}
        />
      )}

      <section id="products" className="scroll-mt-24">
        {!landing && <h1 className="mb-4 text-2xl font-bold tracking-tight">{t('title')}</h1>}

        <CatalogFilters
          searchTerm={searchTerm}
          categoryId={categoryId}
          sortBy={sortBy}
          minPrice={minPrice}
          maxPrice={maxPrice}
          inStock={inStock}
          categories={categories.data ?? []}
          onChange={update}
        />

        {products.isError && <ErrorMessage>{t('loadFailed')}</ErrorMessage>}
        {products.isPending && <LoadingRows rows={4} />}

        {page && (
          <>
            <p className="text-muted-foreground mb-4 text-sm">
              {page.totalCount === 0
                ? t('nothingMatches')
                : searchTerm
                  ? t('resultCountFor', { count: page.totalCount, term: searchTerm })
                  : t('resultCount', { count: page.totalCount })}
            </p>

            {/* Two to a row on a phone (specs/122, #245): 15rem cards fit one per row in 358px, which made twelve
                products a 6,450px page. From `sm` up the cards fill the width as before. */}
            <ul className="grid grid-cols-2 gap-3 sm:grid-cols-[repeat(auto-fill,minmax(15rem,1fr))] sm:gap-4" data-testid="catalogue-grid">
              {page.items.map((product) => (
                <li key={product.id}>
                  <ProductCard product={product} categoryName={categoryName(product.categoryId)} />
                </li>
              ))}
            </ul>

            <Pager
              page={page.pageNumber}
              pageSize={PAGE_SIZE}
              totalCount={page.totalCount}
              onChange={(next) => update({ page: String(next) })}
            />
          </>
        )}
      </section>
    </>
  )
}

/** A bound from the address: a number, or nothing - a typo in a shared link is no bound, not a 400. */
function bound(value: string): number | undefined {
  const n = Number(value)
  return value.trim() !== '' && Number.isFinite(n) && n >= 0 ? n : undefined
}
