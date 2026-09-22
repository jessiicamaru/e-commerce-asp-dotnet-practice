import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { AddToCart } from '@/components/product/add-to-cart'
import { Availability } from '@/components/product/availability'
import { ProductImage } from '@/components/product/product-image'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { VariantChooser } from '@/components/product/variant-chooser'
import { useProduct } from '@/hooks/product'
import { money } from '@/utils/shared'

export function ProductPage() {
  const { id = '' } = useParams()
  const { data: product, isPending, error } = useProduct(id)
  const [chosenVariantId, setChosenVariantId] = useState<string | null>(null)

  if (error) {
    const status = ApiError.from(error).status
    return <ErrorMessage>{status === 404 ? 'This product does not exist.' : 'The product could not be loaded.'}</ErrorMessage>
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
      <p className="mb-4">
        <Link to="/" className="text-sm underline">
          ← Back to the shop
        </Link>
      </p>
      <div className="grid gap-8 md:grid-cols-2">
        <ProductImage product={product} large />
        <div className="flex flex-col gap-3">
          <h1 className="text-2xl font-bold">{product.name}</h1>
          <p className="text-2xl font-semibold">
            {!variant && product.priceVaries && (
              <span className="text-muted-foreground text-base font-normal">from </span>
            )}
            {money(variant?.price ?? product.price)}
          </p>
          <p className="text-muted-foreground text-xs">
            Price excludes tax, which is added at checkout for your delivery country.
          </p>
          <Availability value={variant?.availability ?? product.availability} />
          <VariantChooser variants={sellable} selectedId={chosenVariantId} onSelect={setChosenVariantId} />
          <AddToCart
            productId={product.id}
            variantId={variant?.id}
            disabledReason={variant ? undefined : 'Choose an option first.'}
          />
          {product.description && <p className="text-sm">{product.description}</p>}
          <p className="text-muted-foreground text-xs">SKU {variant?.sku ?? product.sku}</p>
        </div>
      </div>
    </section>
  )
}
