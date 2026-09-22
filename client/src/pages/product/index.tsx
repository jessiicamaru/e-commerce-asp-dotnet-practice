import { Link, useParams } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { AddToCart } from '@/components/product/add-to-cart'
import { Availability } from '@/components/product/availability'
import { ProductImage } from '@/components/product/product-image'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { useProduct } from '@/hooks/product'
import { money } from '@/utils/shared'

export function ProductPage() {
  const { id = '' } = useParams()
  const { data: product, isPending, error } = useProduct(id)

  if (error) {
    const status = ApiError.from(error).status
    return <ErrorMessage>{status === 404 ? 'This product does not exist.' : 'The product could not be loaded.'}</ErrorMessage>
  }

  if (isPending || !product) {
    return <LoadingRows rows={2} />
  }

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
          <p className="text-2xl font-semibold">{money(product.price)}</p>
          <p className="text-muted-foreground text-xs">
            Price excludes tax, which is added at checkout for your delivery country.
          </p>
          <Availability value={product.availability} />
          <AddToCart productId={product.id} />
          {product.description && <p className="text-sm">{product.description}</p>}
          <p className="text-muted-foreground text-xs">SKU {product.sku}</p>
        </div>
      </div>
    </section>
  )
}
