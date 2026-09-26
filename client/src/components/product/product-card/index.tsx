import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ProductImage } from '@/components/product/product-image'
import { SaveButton } from '@/components/product/save-button'
import { StarRating } from '@/components/product/star-rating'
import { Price } from '@/components/shared/price'
import type { Product } from '@/services/product/types'

/**
 * One product in a grid: the picture first, then what it is, then what it costs.
 *
 * <p>
 * The card is the whole link. A shopper aiming at a small product name is a shopper who misses, and
 * two separate links to the same place inside one card is two tab stops for one destination.
 * </p>
 * <p>
 * The <b>sold by</b> line is here and shows the shop itself until sellers exist (specs/026). It is
 * laid out now on purpose: adding a line under the name later moves every card in every grid, and
 * "who am I buying from" is not information a marketplace can bolt on at the end.
 * </p>
 */
export function ProductCard({ product, categoryName }: { product: Product; categoryName?: string }) {
  const { t } = useTranslation('catalog')
  const inStock = product.availability === 'InStock'

  return (
    // The heart sits ON the card, beside the link rather than inside it: a button inside a link is two
    // controls in one and invalid HTML (specs/075).
    <div className="relative h-full">
    <Link
      to={`/products/${product.id}`}
      className="group bg-card ring-border/60 hover:ring-primary/60 flex h-full flex-col gap-3 rounded-3xl p-3 ring-1 transition-all hover:-translate-y-0.5 hover:shadow-lg"
    >
      <div className="relative">
        <ProductImage product={product} />

        {!inStock && (
          <span className="bg-card/90 text-muted-foreground absolute top-3 left-3 rounded-full px-2.5 py-1 text-xs font-medium backdrop-blur">
            {t('product.outOfStock')}
          </span>
        )}
      </div>

      <div className="flex flex-1 flex-col gap-1 px-2 pb-2">
        {categoryName && (
          <span className="text-muted-foreground text-xs">{categoryName}</span>
        )}

        <h3 className="leading-snug font-semibold group-hover:underline">{product.name}</h3>

        <span className="text-muted-foreground text-xs">
          {t('product.soldBy', { seller: product.sellerName ?? t('product.theShop') })}
        </span>

        {product.ratingCount > 0 && product.ratingAverage !== null && (
          <span className="text-muted-foreground flex items-center gap-1.5 text-xs">
            <StarRating value={product.ratingAverage} label={t('reviews.average', { average: product.ratingAverage.toFixed(1) })} />
            {product.ratingAverage.toFixed(1)} · {product.ratingCount}
          </span>
        )}

        <div className="mt-auto flex items-baseline gap-1.5 pt-2">
          {product.priceVaries && (
            <span className="text-muted-foreground text-xs">{t('product.from')}</span>
          )}
          <Price value={product.price} currency={product.currency} className="text-lg font-semibold" />
        </div>
      </div>
    </Link>
    <SaveButton productId={product.id} className="absolute top-5 right-5" />
    </div>
  )
}
