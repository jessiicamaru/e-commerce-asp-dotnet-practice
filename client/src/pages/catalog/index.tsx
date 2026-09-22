import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { CatalogFilters } from '@/components/catalog/catalog-filters'
import { ProductCard } from '@/components/product/product-card'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Pager } from '@/components/shared/pager'
import { PRODUCTS_PER_PAGE } from '@/constants/shared'
import { useCategories } from '@/hooks/category'
import { useProducts } from '@/hooks/product'
import type { SortBy } from '@/services/product/types'

/** The listing (#36), driven by the URL so a search can be shared, bookmarked and survives a reload. */
export function CatalogPage() {
  const { t } = useTranslation('catalog')
  const [params, setParams] = useSearchParams()
  const searchTerm = params.get('q') ?? ''
  const categoryId = params.get('category') ?? ''
  const sortBy = (params.get('sort') as SortBy | null) ?? 'name_asc'
  const pageNumber = Number(params.get('page') ?? '1') || 1

  const categories = useCategories()
  const products = useProducts({ pageNumber, pageSize: PRODUCTS_PER_PAGE, searchTerm, categoryId, sortBy })

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
    <section>
      <h1 className="mb-4 text-2xl font-bold">{t('title')}</h1>

      <CatalogFilters
        searchTerm={searchTerm}
        categoryId={categoryId}
        sortBy={sortBy}
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
          <ul className="grid grid-cols-[repeat(auto-fill,minmax(12rem,1fr))] gap-4">
            {page.items.map((product) => (
              <li key={product.id}>
                <ProductCard product={product} categoryName={categoryName(product.categoryId)} />
              </li>
            ))}
          </ul>
          <Pager
            page={page.pageNumber}
            totalPages={page.totalPages}
            onChange={(next) => update({ page: String(next) })}
            previousLabel={t('pager.previous')}
            nextLabel={t('pager.next')}
          />
        </>
      )}
    </section>
  )
}
