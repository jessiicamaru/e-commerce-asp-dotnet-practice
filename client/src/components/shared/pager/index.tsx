import { useTranslation } from 'react-i18next'
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from '@/components/ui/pagination'
import { cn } from '@/utils/shared'
import { pageWindow } from './page-window'

/**
 * Pages of a list, with where you are in it: "Showing 11–20 of 47".
 *
 * Every paged list in the storefront uses this one and the one page size in `constants/shared`, so
 * the product grid, the seller's listings, orders and sales all move the same way.
 *
 * The page links are real links (`?page=3`), so a page can be opened in a new tab or shared; a plain
 * click is intercepted and handed to `onChange`, which is what keeps the rest of the query string.
 */
export function Pager({
  page,
  pageSize,
  totalCount,
  onChange,
  className,
}: {
  page: number
  pageSize: number
  totalCount: number
  onChange: (page: number) => void
  className?: string
}) {
  const { t } = useTranslation()
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  if (totalCount === 0) {
    return null
  }

  const from = (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  const go = (target: number) => (event: React.MouseEvent) => {
    event.preventDefault()
    if (target >= 1 && target <= totalPages && target !== page) {
      onChange(target)
    }
  }

  const edge = (disabled: boolean) =>
    cn('rounded-full', disabled && 'pointer-events-none opacity-40')

  return (
    <div className={cn('mt-8 flex flex-col items-center justify-between gap-3 sm:flex-row', className)}>
      <p className="text-muted-foreground text-sm">{t('pager.showing', { from, to, total: totalCount })}</p>

      {totalPages > 1 && (
        <Pagination className="mx-0 w-auto">
          <PaginationContent className="gap-1">
            <PaginationItem>
              <PaginationPrevious
                href={`?page=${page - 1}`}
                text={t('pager.previous')}
                aria-label={t('pager.previous')}
                aria-disabled={page <= 1}
                className={edge(page <= 1)}
                onClick={go(page - 1)}
              />
            </PaginationItem>

            {pageWindow(page, totalPages).map((n, index) =>
              n === '…' ? (
                <PaginationItem key={`gap-${index}`}>
                  <PaginationEllipsis />
                </PaginationItem>
              ) : (
                <PaginationItem key={n}>
                  <PaginationLink
                    href={`?page=${n}`}
                    isActive={n === page}
                    aria-label={t('pager.page', { page: n })}
                    className={cn('rounded-full', n === page && 'bg-primary text-primary-foreground border-transparent hover:bg-primary/90')}
                    onClick={go(n)}
                  >
                    {n}
                  </PaginationLink>
                </PaginationItem>
              ),
            )}

            <PaginationItem>
              <PaginationNext
                href={`?page=${page + 1}`}
                text={t('pager.next')}
                aria-label={t('pager.next')}
                aria-disabled={page >= totalPages}
                className={edge(page >= totalPages)}
                onClick={go(page + 1)}
              />
            </PaginationItem>
          </PaginationContent>
        </Pagination>
      )}
    </div>
  )
}
