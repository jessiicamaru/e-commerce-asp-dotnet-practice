import { useTranslation } from 'react-i18next'
import { useParams, useSearchParams } from 'react-router-dom'
import { PauseCircleIcon, StoreIcon } from 'lucide-react'
import { ProductCard } from '@/components/product/product-card'
import { Pager } from '@ecommerce/core/components/shared/pager'
import { PublicVouchers } from '@/components/voucher/public-vouchers'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { useProducts } from '@ecommerce/core/hooks/product'
import { useShopFront } from '@ecommerce/core/hooks/shop'
import { StarRating } from '@ecommerce/core/components/product/star-rating'

/**
 * A shop's page (specs/099): its name and the seller's own words, then what it has on the shelf, paged like the catalogue.
 * A closed or unknown shop is the server's 404, shown as "not found". The description is shown as text, never as markup.
 * A paused shop (specs/107) answers and says its seller is away; staff close a shop from here. Under the name, how its
 * products are rated (specs/165) - or that nobody has reviewed them yet, never zero stars.
 */
export function ShopFrontPage() {
  const { t } = useTranslation('catalog')
  const { sellerId = '' } = useParams()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const shop = useShopFront(sellerId)
  const products = useProducts({ pageNumber: page, pageSize: PAGE_SIZE, sellerId })

  if (shop.isError) {
    return <ErrorMessage>{t('shop.notFound')}</ErrorMessage>
  }

  return (
    <section className="grid gap-6">
      {shop.isPending ? (
        <LoadingRows />
      ) : (
        <header className="bg-card ring-border/60 grid gap-2 rounded-3xl p-6 ring-1">
          <h1 className="flex items-center gap-2 text-3xl font-bold tracking-tight">
            <StoreIcon className="size-7" /> {shop.data.shopName}
          </h1>
          {shop.data.ratingCount > 0 && shop.data.ratingAverage !== null ? (
            <p className="flex items-center gap-2 text-sm">
              <StarRating
                value={shop.data.ratingAverage}
                label={t('shop.rating', { average: shop.data.ratingAverage.toFixed(1) })}
              />
              <span className="font-medium">{shop.data.ratingAverage.toFixed(1)}</span>
              <span className="text-muted-foreground">{t('reviews.count', { count: shop.data.ratingCount })}</span>
            </p>
          ) : (
            <p className="text-muted-foreground text-sm">{t('shop.noReviews')}</p>
          )}
          {shop.data.description && <p className="text-muted-foreground whitespace-pre-line">{shop.data.description}</p>}
          {shop.data.paused ? (
            <p className="flex items-center gap-2 text-sm font-medium" role="status">
              <PauseCircleIcon className="size-4" /> {t('shop.paused')}
            </p>
          ) : (
            <p className="text-sm">{t('shop.onSale', { count: shop.data.productCount })}</p>
          )}
        </header>
      )}

      {shop.data && !shop.data.paused && <PublicVouchers scope={{ sellerIds: [shop.data.sellerId] }} />}

      {products.isError ? (
        <ErrorMessage>{t('shop.productsFailed')}</ErrorMessage>
      ) : products.isPending || !products.data ? (
        <LoadingRows />
      ) : (
        <>
          <ul className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
            {products.data.items.map((product) => (
              <li key={product.id}>
                <ProductCard product={product} />
              </li>
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={products.data.totalCount}
            onChange={(next) => setParams(next > 1 ? { page: String(next) } : {})}
          />
        </>
      )}
    </section>
  )
}
