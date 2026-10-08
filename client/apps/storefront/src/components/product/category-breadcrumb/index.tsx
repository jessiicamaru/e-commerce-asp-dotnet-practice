import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ChevronRightIcon } from 'lucide-react'
import { useCategories } from '@ecommerce/core/hooks/category'
import { categoryPath } from '@ecommerce/core/utils/category'

/**
 * Where a product sits - "Điện tử › Điện thoại" - each a link to the listing filtered by it (specs/158, #363).
 *
 * <p>
 * From the category list the query layer already holds (the filter asked for it, kept five minutes), so opening a
 * product costs no request for this. Nothing is drawn while that list is unknown, rather than a lone guess.
 * </p>
 */
export function CategoryBreadcrumb({ categoryId }: { categoryId: string }) {
  const { t } = useTranslation('catalog')
  const categories = useCategories()
  const path = categoryPath(categories.data ?? [], categoryId)

  if (path.length === 0) return null

  return (
    <nav aria-label={t('product.breadcrumb')} className="text-muted-foreground mb-1 flex flex-wrap items-center gap-1 text-sm">
      {path.map((category, index) => (
        <Fragment key={category.id}>
          {index > 0 && <ChevronRightIcon aria-hidden="true" className="size-3.5" />}
          <Link to={`/?category=${category.id}`} className="hover:text-foreground hover:underline">
            {category.name}
          </Link>
        </Fragment>
      ))}
    </nav>
  )
}
