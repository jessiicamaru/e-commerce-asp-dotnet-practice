import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { ProductImage } from '@/components/product/product-image'
import { Pager } from '@/components/shared/pager'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useAuth } from '@/context/auth/useAuth'
import { useMyProducts } from '@/hooks/product'
import { useMyShop, useRenameShop } from '@/hooks/seller'

const PER_PAGE = 12

/**
 * A seller's own listings (specs/028).
 *
 * <p>
 * <b>Nothing on this page names a seller.</b> The request carries a token and the server answers
 * with that seller's products; there is no id to tamper with, in the address bar or anywhere else.
 * </p>
 * <p>
 * Seeing a shop here is a convenience, not a permission: `RequireRole` decides what to draw and the
 * server decides what to allow. Somebody who forces their way onto this page sees an empty list,
 * because the server answers the token rather than the route.
 * </p>
 */
export function ShopPage() {
  const { t } = useTranslation('seller')
  const { isSeller } = useAuth()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1

  const shop = useMyShop(isSeller)
  const listings = useMyProducts({ pageNumber: page, pageSize: PER_PAGE }, isSeller)

  const rename = useRenameShop()
  const [newName, setNewName] = useState('')

  if (listings.isError) {
    return <ErrorMessage>{t('listing.loadFailed')}</ErrorMessage>
  }

  if (listings.isPending || !listings.data) {
    return <LoadingRows />
  }

  const { items, totalCount, totalPages } = listings.data

  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-bold">{shop.data?.shopName ?? t('title')}</h1>
          <Link
            to="/shop/sales"
            className="ring-border hover:bg-muted rounded-full px-4 py-2 text-sm font-semibold ring-1 transition-colors"
          >
            {t('sales.link')}
          </Link>
        </div>
        <p className="text-muted-foreground text-sm">{t('subtitle')}</p>
      </header>

      <form
        className="bg-card ring-border/60 grid gap-2 rounded-3xl p-4 ring-1"
        onSubmit={(event) => {
          event.preventDefault()
          const wanted = newName.trim()
          if (wanted) {
            rename.mutate(wanted, { onSuccess: () => setNewName('') })
          }
        }}
      >
        <label htmlFor="shopName" className="text-sm font-semibold">
          {t('shopName')}
        </label>
        <div className="flex flex-wrap gap-2">
          <Input
            id="shopName"
            name="shopName"
            value={newName}
            placeholder={shop.data?.shopName ?? ''}
            onChange={(event) => setNewName(event.target.value)}
            className="h-9 flex-1 rounded-full"
          />
          <Button type="submit" size="sm" className="rounded-full" disabled={rename.isPending}>
            {t('rename')}
          </Button>
        </div>
        {/* Said out loud because it is surprising: one row changes two hundred listings. */}
        <p className="text-muted-foreground text-xs">{t('renameHint')}</p>
        <ServerError error={rename.error} fallback={t('listing.loadFailed')} />
      </form>

      {totalCount === 0 ? (
        <div className="bg-card ring-border/60 grid justify-items-start gap-3 rounded-3xl p-8 ring-1">
          <h2 className="text-lg font-semibold">{t('empty.title')}</h2>
          <p className="text-muted-foreground max-w-prose text-sm">{t('empty.body')}</p>
          <Link
            to="/shop/products/new"
            className="bg-primary text-primary-foreground rounded-full px-4 py-2 text-sm font-semibold transition-opacity hover:opacity-90"
          >
            {t('empty.action')}
          </Link>
        </div>
      ) : (
        <>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-muted-foreground text-sm">{t('listing.count', { count: totalCount })}</p>
            <Link
              to="/shop/products/new"
              className="bg-primary text-primary-foreground rounded-full px-4 py-2 text-sm font-semibold transition-opacity hover:opacity-90"
            >
              {t('listing.new')}
            </Link>
          </div>

          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {items.map((product) => (
              <li key={product.id}>
                <Link
                  to={`/shop/products/${product.id}`}
                  className="group bg-card ring-border/60 hover:ring-primary/60 flex h-full flex-col gap-3 rounded-3xl p-3 ring-1 transition-all hover:-translate-y-0.5 hover:shadow-lg"
                >
                  <ProductImage product={product} />
                  <div className="grid gap-1 px-1 pb-1">
                    <p className="line-clamp-2 font-semibold">{product.name}</p>
                    <p className="text-muted-foreground text-xs">{product.sku}</p>
                    <Price value={product.price} currency={product.currency} className="font-semibold" />
                    {product.availability !== 'InStock' && (
                      <p className="text-muted-foreground text-xs">{t('listing.outOfStock')}</p>
                    )}
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          <Pager
            page={page}
            totalPages={totalPages}
            onChange={(next) => setParams({ page: String(next) })}
          />
        </>
      )}
    </section>
  )
}
