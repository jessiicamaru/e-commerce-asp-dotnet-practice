import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { Pager } from '@/components/shared/pager'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { useAuth } from '@/context/auth/useAuth'
import { useMySales } from '@/hooks/order'
import { describeSaleStatus } from './status'

const PER_PAGE = 10

/**
 * The orders that include something this seller listed (specs/034), newest first.
 *
 * <p>
 * <b>Every amount is the seller's part of the order</b>, never the order's total: on an order that
 * mixes sellers the total includes goods that are not theirs, and delivery and tax belong to the
 * whole order. The page says so, because "your lines" next to a number reads as revenue otherwise.
 * </p>
 * <p>
 * Nothing in the request names the seller. The server answers the token, not this page.
 * </p>
 */
export function ShopSalesPage() {
  const { t, i18n } = useTranslation('seller')
  const { isSeller } = useAuth()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const sales = useMySales(page, PER_PAGE, isSeller)

  if (sales.isError) {
    return <ErrorMessage>{t('sales.loadFailed')}</ErrorMessage>
  }

  if (sales.isPending || !sales.data) {
    return <LoadingRows />
  }

  const { items, totalCount } = sales.data

  return (
    <section className="grid gap-6">
      <header className="grid gap-1">
        <Link to="/shop" className="text-muted-foreground text-sm hover:underline">
          ← {t('title')}
        </Link>
        <h1 className="text-2xl font-bold">{t('sales.title')}</h1>
        <p className="text-muted-foreground max-w-prose text-sm">{t('sales.subtitle')}</p>
      </header>

      {totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('sales.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {items.map((sale) => (
              <li key={sale.orderId}>
                <Link
                  to={`/shop/sales/${sale.orderId}`}
                  className="bg-card ring-border/60 hover:ring-primary/60 grid gap-1 rounded-3xl p-4 ring-1 transition-all"
                >
                  <span className="font-medium">
                    {t('sales.placedAt', { at: new Date(sale.createdAt).toLocaleString(i18n.language) })}
                  </span>
                  <span className="text-sm">
                    {t('sales.lines', { count: sale.lineCount })} · {t('sales.units', { count: sale.units })} ·{' '}
                    <Price value={sale.subtotal} currency={sale.currency} className="font-semibold" />
                  </span>
                  <span className="text-muted-foreground text-xs">{describeSaleStatus(t, sale.status)}</span>
                </Link>
              </li>
            ))}
          </ul>

          <Pager
            page={page}
            totalPages={Math.max(1, Math.ceil(totalCount / PER_PAGE))}
            onChange={(next) => setParams({ page: String(next) })}
            previousLabel={t('sales.newer')}
            nextLabel={t('sales.older')}
          />
        </>
      )}
    </section>
  )
}
