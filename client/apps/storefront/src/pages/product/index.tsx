import { useEffect, useRef, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { ApiError } from '@ecommerce/core/config/axios'
import { AddToCart } from '@/components/product/add-to-cart'
import { SaveButton } from '@/components/product/save-button'
import { ReportButton } from '@/components/report/report-button'
import { StockBadge } from '@/components/product/stock-badge'
import { ProductImage } from '@/components/product/product-image'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { VariantChooser } from '@/components/product/variant-chooser'
import { useProduct } from '@ecommerce/core/hooks/product'
import { Product } from '@ecommerce/core/services/product'
import { useVariantStock } from '@ecommerce/core/hooks/stock'
import { Price } from '@/components/shared/price'
import { StarRating } from '@/components/product/star-rating'
import { ProductQuestions } from '@/components/product/product-questions'
import { ProductReviews } from '@/components/product/product-reviews'
import { PublicVouchers } from '@/components/voucher/public-vouchers'

export function ProductPage() {
  const { t } = useTranslation('catalog')
  const { id = '' } = useParams()
  // The choice survives a trip through sign-in (specs/126): Add to cart, signed out, returns here with ?variant=.
  const [params] = useSearchParams()
  const { data: product, isPending, error } = useProduct(id)
  const [chosenVariantId, setChosenVariantId] = useState<string | null>(() => params.get('variant'))
  // One view per product opened (specs/047) - not per render, not per refetch when the tab regains focus.
  // Fire and forget: a view that fails to count is not worth an error on the page.
  const counted = useRef<string | null>(null)
  useEffect(() => {
    if (!id || counted.current === id) return
    counted.current = id
    Product.recordView(id).catch(() => {})
  }, [id])
  // Inventory's real count for every shape, not Catalog's in-stock flag: "2 left" is the thing a
  // shopper deciding between two kits wants to know, and the plus button stops there.
  const stock = useVariantStock((product?.variants ?? []).filter((v) => v.isActive).map((v) => v.id))

  if (error) {
    const status = ApiError.from(error).status
    return <ErrorMessage>{status === 404 ? t('product.notFound') : t('product.loadFailed')}</ErrorMessage>
  }

  if (isPending || !product) {
    return <LoadingRows rows={2} />
  }

  // A product sold in one shape has nothing to choose: that variant is it. With several, nothing is
  // chosen until the customer says so (specs/020 research D10).
  const sellable = (product.variants ?? []).filter((candidate) => candidate.isActive)
  const variant = sellable.length === 1 ? sellable[0] : sellable.find((candidate) => candidate.id === chosenVariantId)

  return (
    <section>
      <p className="mb-6">
        <Link to="/" className="text-muted-foreground hover:text-foreground text-sm transition-colors">
          {t('product.back')}
        </Link>
      </p>

      <div className="grid gap-8 lg:grid-cols-[1.1fr_1fr]">
        {/* The picture gets its own panel rather than floating on the page: an object on a surface
            reads as a photographed thing, which is most of what this redesign is doing. */}
        <div className="bg-card ring-border/60 rounded-[2rem] p-4 ring-1">
          {/* The picture follows the choice (specs/032). `variant.imageUrl` is the variant's own
              or the product's, folded together by the server, so this needs no fallback of its
              own - and cannot get one wrong. */}
          <ProductImage product={product} imageUrl={variant?.imageUrl} large />
        </div>

        <div className="flex flex-col gap-4 lg:pt-4">
          <div>
            <div className="flex items-start justify-between gap-3">
              <h1 className="text-3xl font-bold tracking-tight text-balance">{product.name}</h1>
              <div className="flex shrink-0 items-center gap-1">
                <SaveButton productId={product.id} className="shrink-0" />
                <ReportButton targetType="Product" targetId={product.id} />
              </div>
            </div>
            <p className="text-muted-foreground mt-1 text-sm">
              <Trans
                t={t}
                i18nKey="product.soldByShop"
                values={{ seller: product.sellerName ?? t('product.theShop') }}
                components={{
                  shop: product.sellerId ? (
                    <Link to={`/shops/${product.sellerId}`} className="text-foreground font-medium hover:underline" />
                  ) : (
                    <span />
                  ),
                }}
              />
            </p>
            {product.ratingCount > 0 && product.ratingAverage !== null && (
              <a href="#reviews" className="mt-2 inline-flex items-center gap-2 text-sm hover:underline">
                <StarRating value={product.ratingAverage} label={t('reviews.average', { average: product.ratingAverage.toFixed(1) })} />
                <span className="text-muted-foreground">{t('reviews.count', { count: product.ratingCount })}</span>
              </a>
            )}
          </div>

          <div className="flex flex-wrap items-baseline gap-2">
            {!variant && product.priceVaries && (
              <span className="text-muted-foreground text-sm">{t('product.from')}</span>
            )}
            <Price
              value={variant ? variant.price : product.price}
              currency={variant?.currency ?? product.currency}
              className="text-3xl font-bold"
            />
            <span className="text-muted-foreground text-xs">{t('product.taxNote')}</span>
          </div>

          {variant && (
            <StockBadge
              available={stock.byVariant[variant.id]?.quantityAvailable}
              pending={stock.isPending}
              className="-mt-1"
            />
          )}

          <VariantChooser
            variants={sellable}
            selectedId={chosenVariantId}
            onSelect={setChosenVariantId}
            stock={stock.byVariant}
            stockPending={stock.isPending}
          />

          <AddToCart
            productId={product.id}
            variantId={variant?.id}
            available={variant ? stock.byVariant[variant.id]?.quantityAvailable : undefined}
            disabledReason={variant ? undefined : t('product.chooseFirst')}
          />

          <PublicVouchers
            scope={{
              platform: true,
              sellerIds: product.sellerId ? [product.sellerId] : [],
              productId: product.id,
              variantIds: sellable.map((v) => v.id),
            }}
          />

          {product.description && (
            <p className="text-muted-foreground text-sm leading-relaxed">{product.description}</p>
          )}

          {/* Only a chosen variant's: with nothing chosen the first one's SKU named a shape nobody picked (specs/126). */}
          {variant && <p className="text-muted-foreground border-t pt-4 text-xs">{t('product.sku', { sku: variant.sku })}</p>}
        </div>
      </div>

      <ProductReviews product={product} />
      <ProductQuestions product={product} />
    </section>
  )
}
