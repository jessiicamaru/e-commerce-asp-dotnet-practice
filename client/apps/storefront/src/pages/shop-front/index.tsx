import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useParams, useSearchParams } from 'react-router-dom'
import { PauseCircleIcon, StoreIcon } from 'lucide-react'
import { ProductCard } from '@/components/product/product-card'
import { CloseShop } from '@/components/admin/close-shop'
import { Pager } from '@/components/shared/pager'
import { PublicVouchers } from '@/components/voucher/public-vouchers'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useProducts } from '@ecommerce/core/hooks/product'
import { Shops } from '@ecommerce/core/services/shops'

/**
 * A shop's page (specs/099): its name and the seller's own words, then what it has on the shelf, paged like the catalogue.
 * A closed or unknown shop is the server's 404, shown as "not found". The description is shown as text, never as markup.
 * A paused shop (specs/107) answers and says its seller is away; staff close a shop from here.
 */
export function ShopFrontPage() {
  const { t } = useTranslation('catalog')
  const { sellerId = '' } = useParams()
  const { isStaff } = useAuth()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const shop = useQuery({ queryKey: queryKeys.shopFront(sellerId), queryFn: () => Shops.get(sellerId), retry: false })
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
          {shop.data.description && <p className="text-muted-foreground whitespace-pre-line">{shop.data.description}</p>}
          {shop.data.paused ? (
            <p className="flex items-center gap-2 text-sm font-medium" role="status">
              <PauseCircleIcon className="size-4" /> {t('shop.paused')}
            </p>
          ) : (
            <p className="text-sm">{t('shop.onSale', { count: shop.data.productCount })}</p>
          )}
          {isStaff && <CloseShop sellerId={shop.data.sellerId} shopName={shop.data.shopName} />}
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
