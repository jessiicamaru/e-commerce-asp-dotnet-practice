import { useState } from 'react'
import { cn } from '@/utils/shared'
import type { Product } from '@/services/product/types'

/**
 * The product's picture, or a letter tile when it has none (specs/019) - or when the picture fails to
 * load, so a broken image never shows. Served by Catalog through the gateway, like every call.
 */
export function ProductImage({ product, large = false }: { product: Product; large?: boolean }) {
  const [failed, setFailed] = useState<string | null>(null)
  const shape = cn('aspect-[4/3] w-full rounded-md bg-muted', large && 'text-6xl')

  if (!product.imageUrl || failed === product.imageUrl) {
    return (
      <div className={cn(shape, 'text-muted-foreground grid place-items-center text-2xl font-bold')} aria-hidden="true">
        {product.name.charAt(0)}
      </div>
    )
  }

  return (
    <img
      className={cn(shape, 'object-contain')}
      src={product.imageUrl}
      alt={product.name}
      loading="lazy"
      onError={() => setFailed(product.imageUrl)}
    />
  )
}
