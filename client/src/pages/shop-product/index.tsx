import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ProductImage } from '@/components/product/product-image'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { CURRENCIES } from '@/config/money'
import {
  useDeleteProduct,
  useProductInEveryCurrency,
  useSetVariantPrice,
  useUploadProductImage,
} from '@/hooks/product'

/**
 * One of the seller's own listings, and the three things they can do to it (specs/028).
 *
 * <p>
 * <b>It is read through the ordinary product endpoint</b>, the same one a shopper uses. There is no
 * seller-scoped read of a single product and there should not be: the writes below are what is
 * guarded, by `SellerOwnership`, which answers <b>404</b> for somebody else's listing - the same
 * answer as a product that does not exist, on purpose. So a seller who opens another seller's id
 * sees the page and is refused the moment they try to change anything, which is the behaviour the
 * server is deliberately specifying.
 * </p>
 * <p>
 * The price editor shows <b>every</b> currency the shop prices in, not just the active one, because
 * this is the page where the missing second price from the create form gets filled in - and a price
 * that exists in one currency and not the other is the single most confusing state specs/022 can
 * produce.
 * </p>
 */
export function SellerProductPage() {
  const { t } = useTranslation('seller')
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const product = useProductInEveryCurrency(id)

  const setPrice = useSetVariantPrice(id)
  const upload = useUploadProductImage(id)
  const remove = useDeleteProduct(id)

  const [amounts, setAmounts] = useState<Record<string, string>>({})

  if (product.isError) {
    return <ErrorMessage>{t('listing.loadFailed')}</ErrorMessage>
  }

  if (product.isPending || !product.product) {
    return <LoadingRows />
  }

  const item = product.product
  const variants = item.variants ?? []

  return (
    <section className="mx-auto grid max-w-3xl gap-6">
      <header className="grid gap-1">
        <Link to="/shop" className="text-muted-foreground text-sm underline">
          {t('title')}
        </Link>
        <h1 className="text-2xl font-bold">{item.name}</h1>
        <p className="text-muted-foreground text-sm">{item.sku}</p>
      </header>

      {/* minmax(0, 220px), not 220px: a fixed track does not shrink, and a file input has a large
          intrinsic width - it pushed this column to 313px and drew the image straight over the
          price editor. Seen in a screenshot, which is the only way this kind of thing is seen. */}
      <div className="bg-card ring-border/60 grid gap-4 rounded-3xl p-6 ring-1 sm:grid-cols-[minmax(0,220px)_1fr]">
        <div className="grid min-w-0 gap-2">
          <ProductImage product={item} />
          <label className="text-sm font-semibold" htmlFor="image">
            {t('edit.image')}
          </label>
          <input
            id="image"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="w-full min-w-0 text-sm"
            onChange={(event) => {
              const file = event.target.files?.[0]
              if (file) {
                upload.mutate(file)
              }
            }}
          />
          <ServerError error={upload.error} fallback={t('listing.loadFailed')} />
        </div>

        <div className="grid min-w-0 content-start gap-4">
          <h2 className="font-semibold">{t('edit.price')}</h2>

          {variants.map((variant) => (
            <div key={variant.id} className="grid gap-2">
              {variant.optionSummary && (
                <p className="text-muted-foreground text-xs">{variant.optionSummary}</p>
              )}
              {CURRENCIES.map((currency) => {
                const key = `${variant.id}:${currency}`
                // The price in THIS currency, read from that currency's own response - not from the
                // one the seller happens to be browsing in. A variant nobody priced here is null,
                // which is a real state and not a zero.
                const current = product.byCurrency[currency]?.[variant.id] ?? null

                return (
                  <div key={currency} className="flex flex-wrap items-center gap-2">
                    <span className="w-12 text-sm font-semibold">{currency}</span>
                    <Input
                      inputMode="decimal"
                      className="h-9 w-40 rounded-full"
                      aria-label={`${currency} ${variant.sku}`}
                      value={amounts[key] ?? (current ?? '')}
                      onChange={(event) => setAmounts((p) => ({ ...p, [key]: event.target.value }))}
                    />
                    <Button
                      size="sm"
                      className="rounded-full"
                      disabled={setPrice.isPending}
                      onClick={() =>
                        setPrice.mutate({ variantId: variant.id, currency, amount: Number(amounts[key] ?? current ?? 0) })
                      }
                    >
                      {t('edit.savePrice')}
                    </Button>
                    {current === null && (
                      <span className="text-muted-foreground text-xs">{t('listing.noPrice')}</span>
                    )}
                  </div>
                )
              })}
            </div>
          ))}

          <ServerError error={setPrice.error} fallback={t('listing.loadFailed')} />

          <Link to={`/products/${item.id}`} className="text-sm underline">
            {t('edit.view')}
          </Link>
        </div>
      </div>

      <div className="grid justify-items-start gap-2">
        <Button
          variant="destructive"
          className="rounded-full"
          disabled={remove.isPending}
          onClick={() => {
            if (window.confirm(t('edit.withdrawConfirm', { name: item.name }))) {
              remove.mutate(undefined as never, { onSuccess: () => navigate('/shop') })
            }
          }}
        >
          {t('edit.withdraw')}
        </Button>
        <ServerError error={remove.error} fallback={t('listing.loadFailed')} />
      </div>
    </section>
  )
}
