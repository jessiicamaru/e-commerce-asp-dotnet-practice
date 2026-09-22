import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ProductImage } from '@/components/product/product-image'
import { Price } from '@/components/shared/price'
import type { Category } from '@/services/category/types'
import type { Product } from '@/services/product/types'

/** Enough to steer by. A wall of chips is a filter, and there is already a filter. */
const CHIPS = 6

/**
 * The first thing a visitor sees, and the only part of this storefront that is decoration.
 *
 * <p>
 * <b>Everything in it is true.</b> The reference design the owner sent carries "5m+ reviews" and
 * "new gen" badges; this shop has no reviews and no way to know what is new, so it says neither. A
 * count of products is a fact, a category is a fact, and one real camera at its real price is a
 * fact. Invented social proof on a shop that has never sold anything is the one thing a storefront
 * must not do.
 * </p>
 * <p>
 * Shown only on the unfiltered landing view: once somebody has searched, the results are what they
 * came for and a hero is in the way.
 * </p>
 */
export function CatalogHero({
  productCount,
  categories,
  featured,
  onCategory,
}: {
  productCount: number
  categories: Category[]
  featured?: Product
  onCategory: (id: string) => void
}) {
  const { t } = useTranslation('catalog')

  return (
    <section className="mb-10 grid items-start gap-4 lg:grid-cols-3">
      <div className="from-primary/30 via-primary/10 relative grid gap-6 overflow-hidden rounded-[2rem] bg-linear-to-br to-transparent p-8 sm:grid-cols-[1.2fr_1fr] sm:items-center lg:col-span-2">
        <div className="flex flex-col gap-6">
          <div>
            <p className="text-muted-foreground text-sm font-medium">{t('hero.eyebrow')}</p>
            <h1 className="mt-2 text-4xl leading-[1.05] font-bold tracking-tight text-balance sm:text-5xl">
              {t('hero.heading')}
            </h1>
          </div>

          <div className="flex flex-wrap items-center gap-4">
            <a
              href="#products"
              className="bg-foreground text-background rounded-full px-5 py-2.5 text-sm font-semibold transition-opacity hover:opacity-90"
            >
              {t('hero.browse')}
            </a>
            <span className="text-muted-foreground text-sm">
              {t('resultCount', { count: productCount })}
            </span>
          </div>
        </div>

        {/* The featured camera sits IN the panel rather than beside it: a hero with a hole where a
            product should be was the first thing wrong with this, and only looking showed it. */}
        {featured && (
          <Link to={`/products/${featured.id}`} className="group block">
            <div className="transition-transform group-hover:-rotate-1 group-hover:scale-[1.02]">
              <ProductImage product={featured} large />
            </div>
            <p className="mt-3 truncate text-sm font-semibold group-hover:underline">{featured.name}</p>
            <Price value={featured.price} currency={featured.currency} className="text-muted-foreground text-sm" />
          </Link>
        )}
      </div>

      <div className="bg-card ring-border/60 rounded-[2rem] p-6 ring-1">
        <h2 className="mb-3 text-sm font-semibold">{t('hero.categories')}</h2>
        <div className="flex flex-wrap gap-2">
          {categories.slice(0, CHIPS).map((category) => (
            <button
              key={category.id}
              type="button"
              onClick={() => onCategory(category.id)}
              className="bg-secondary hover:bg-primary hover:text-primary-foreground rounded-full px-3 py-1.5 text-sm transition-colors"
            >
              {category.name}
            </button>
          ))}
        </div>
      </div>
    </section>
  )
}
