import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { OrderRow } from '@/components/order/order-row'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { Badge } from '@ecommerce/ui/badge'
import { useAuth } from '@ecommerce/core/context/auth/useAuth'
import { useMySales } from '@ecommerce/core/hooks/order'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'


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
  const { t } = useTranslation('seller')
  const { isSeller } = useAuth()
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const sales = useMySales(page, PAGE_SIZE, isSeller)

  if (sales.isError) {
    return <ErrorMessage>{t('sales.loadFailed')}</ErrorMessage>
  }

  if (sales.isPending || !sales.data) {
    return <LoadingRows />
  }

  const { items, totalCount } = sales.data

  return (
    <section className="grid gap-6">
      <PageTitle title={t('sales.title')} subtitle={t('sales.subtitle')} />

      {totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('sales.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {items.map((sale) => (
              <li key={sale.orderId}>
                {/* Their own lines by name, the reference and the part's state as a chip (specs/132). */}
                <OrderRow
                  to={`/shop/sales/${sale.orderId}`}
                  orderId={sale.orderId}
                  lines={sale.lines}
                  lineCount={sale.lineCount}
                  createdAt={sale.createdAt}
                  status={sale.status}
                  amount={sale.subtotal}
                  currency={sale.currency}
                  detail={
                    <span className="text-muted-foreground flex flex-wrap items-center gap-2 text-xs">
                      {t('sales.lines', { count: sale.lineCount })} · {t('sales.units', { count: sale.units })}
                      {sale.returnStatus && (
                        <Badge variant="outline">{t('returns.badge', { state: t(`returns.tab.${sale.returnStatus}`) })}</Badge>
                      )}
                    </span>
                  }
                />
              </li>
            ))}
          </ul>

          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            onChange={(next) => setParams({ page: String(next) })}
          />
        </>
      )}
    </section>
  )
}
