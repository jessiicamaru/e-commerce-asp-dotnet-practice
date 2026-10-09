import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from 'cn'
import { ProductImage } from '@ecommerce/core/components/product/product-image'
import type { Product } from '@ecommerce/core/services/product/types'

/**
 * A product's photographs (specs/160, #368): one shown large, every one as a thumbnail beneath - the cover first, then
 * the gallery in its order. On a phone the thumbnails scroll sideways.
 *
 * The chosen shape's own photograph (specs/032) still wins: `variantImageUrl` is shown until the shopper picks a
 * thumbnail, and choosing another shape shows that shape's again. With the cover alone there is no strip at all, so a
 * product with one photograph looks as it always did.
 */
export function ProductGallery({
  product,
  variantImageUrl,
}: {
  product: Pick<Product, 'id' | 'name' | 'imageUrl' | 'photos'>
  /** The chosen variant's picture, the server's fallback already folded in; undefined when none is chosen. */
  variantImageUrl?: string | null
}) {
  const { t } = useTranslation('catalog')
  // A pick belongs to the shape it was made under: choosing another shape shows that shape's photograph again.
  const [picked, setPicked] = useState<{ url: string; under: string | null | undefined } | null>(null)

  const urls = [product.imageUrl, ...(product.photos ?? []).map((photo) => photo.url)].filter(
    (url): url is string => !!url,
  )
  const shown = picked && picked.under === variantImageUrl ? picked.url : (variantImageUrl ?? product.imageUrl)

  return (
    <div className="grid gap-3">
      <ProductImage product={product} imageUrl={shown} large />

      {urls.length > 1 && (
        <ul
          aria-label={t('gallery.label', { name: product.name })}
          className="-mx-1 flex snap-x gap-2 overflow-x-auto px-1 pb-1"
        >
          {urls.map((url, index) => (
            <li key={url} className="w-16 shrink-0 snap-start sm:w-20">
              <button
                type="button"
                aria-label={t('gallery.show', { n: index + 1, count: urls.length })}
                aria-pressed={url === shown}
                onClick={() => setPicked({ url, under: variantImageUrl })}
                className={cn(
                  'block w-full rounded-xl ring-2 ring-transparent transition outline-none',
                  'focus-visible:ring-ring hover:ring-border',
                  url === shown && 'ring-primary hover:ring-primary',
                )}
              >
                <ProductImage product={product} imageUrl={url} thumb />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
