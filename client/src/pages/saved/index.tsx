import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { HeartIcon } from 'lucide-react'
import { ProductCard } from '@/components/product/product-card'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { PAGE_SIZE } from '@/constants/shared'
import { useSavedProducts } from '@/hooks/saved-product'
import { cn } from '@/utils/shared'

/**
 * What the shopper saved for later (specs/075, #109), newest first, at today's price in their currency. Something
 * that can no longer be bought stays, dimmed and saying so, until they unsave it - it was their choice, and a list
 * that silently loses things is one nobody trusts (research D2).
 */
export function SavedPage() {
  const { t } = useTranslation('catalog')
  const [page, setPage] = useState(1)
  const saved = useSavedProducts(page, PAGE_SIZE)

  return (
    <section className="grid gap-6">
      <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight">
        <HeartIcon className="size-6" /> {t('saved.title')}
      </h1>

      {saved.isError ? (
        <ErrorMessage>{t('saved.loadFailed')}</ErrorMessage>
      ) : saved.isPending || !saved.data ? (
        <LoadingRows />
      ) : saved.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">
          {t('saved.none')}{' '}
          <Link to="/" className="underline">
            {t('saved.browse')}
          </Link>
        </p>
      ) : (
        <>
          <ul className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
            {saved.data.items.map((item) => (
              <li key={item.product.id} className={cn('grid gap-1', !item.available && 'opacity-60')}>
                <ProductCard product={item.product} />
                {!item.available && <p className="text-muted-foreground px-2 text-xs">{t('saved.unavailable')}</p>}
              </li>
            ))}
          </ul>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={saved.data.totalCount} onChange={setPage} />
        </>
      )}
    </section>
  )
}
