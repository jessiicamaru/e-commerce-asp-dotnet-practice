import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { AddToCart } from '@/components/product/add-to-cart'
import { Availability } from '@/components/product/availability'
import { ProductImage } from '@/components/product/product-image'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { VariantChooser } from '@/components/product/variant-chooser'
import { useProduct } from '@/hooks/product'
import { Price } from '@/components/shared/price'

export function ProductPage() {
  const { t } = useTranslation('catalog')
  const { id = '' } = useParams()
  const { data: product, isPending, error } = useProduct(id)
  const [chosenVariantId, setChosenVariantId] = useState<string | null>(null)

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
            <h1 className="text-3xl font-bold tracking-tight text-balance">{product.name}</h1>
            <p className="text-muted-foreground mt-1 text-sm">
              {t('product.soldBy', { seller: product.sellerName ?? t('product.theShop') })}
            </p>
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

          <Availability value={variant?.availability ?? product.availability} />

          <VariantChooser variants={sellable} selectedId={chosenVariantId} onSelect={setChosenVariantId} />

          <AddToCart
            productId={product.id}
            variantId={variant?.id}
            disabledReason={variant ? undefined : t('product.chooseFirst')}
          />

          {product.description && (
            <p className="text-muted-foreground text-sm leading-relaxed">{product.description}</p>
          )}

          <p className="text-muted-foreground border-t pt-4 text-xs">
            {t('product.sku', { sku: variant?.sku ?? product.sku })}
          </p>
        </div>
      </div>
    </section>
  )
}
