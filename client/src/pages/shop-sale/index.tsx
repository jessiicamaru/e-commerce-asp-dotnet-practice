import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { OrderLines } from '@/components/order/order-lines'
import { Price } from '@/components/shared/price'
import { LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { useSale } from '@/hooks/order'
import { describeSaleStatus } from '@/pages/shop-sales/status'

/**
 * One sale: the seller's own lines of one order (specs/034).
 *
 * <p>
 * There is no customer, no address and no order total on this page because the server does not send
 * them - a seller who only looks has no use for them, and the address arrives with the job of
 * shipping, not ahead of it.
 * </p>
 * <p>
 * An order that is not this seller's sale is refused exactly like one that does not exist, and the
 * page repeats the server's words rather than guessing which of the two it was.
 * </p>
 */
export function ShopSalePage() {
  const { t, i18n } = useTranslation('seller')
  const { id = '' } = useParams()
  const sale = useSale(id)

  const back = (
    <Link to="/shop/sales" className="text-muted-foreground text-sm hover:underline">
      {t('sales.back')}
    </Link>
  )

  if (sale.isError) {
    return (
      <section className="grid gap-3">
        {back}
        <ServerError error={sale.error} fallback={t('sales.loadFailed')} />
      </section>
    )
  }

  if (sale.isPending) {
    return <LoadingRows />
  }

  const { data } = sale

  return (
    <section className="grid gap-4">
      {back}
      <header className="grid gap-1">
        <h1 className="text-2xl font-bold">
          {t('sales.placedAt', { at: new Date(data.createdAt).toLocaleString(i18n.language) })}
        </h1>
        <p className="text-muted-foreground text-sm">{describeSaleStatus(t, data.status)}</p>
      </header>

      {/* The order's own currency, frozen at checkout - not whatever the seller is browsing in. */}
      <OrderLines items={data.items} currency={data.currency} />

      <div className="grid justify-items-end gap-1">
        <p className="text-sm">
          {t('sales.subtotal')}:{' '}
          <Price value={data.subtotal} currency={data.currency} className="font-semibold" />
        </p>
        <p className="text-muted-foreground max-w-prose text-right text-xs">{t('sales.subtotalHint')}</p>
      </div>
    </section>
  )
}
