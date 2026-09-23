import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { Price } from '@/components/shared/price'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { useFulfilmentQueue } from '@/hooks/admin'
import { QUEUE_STATES, type QueueState } from '@/services/admin/types'
import { PAGE_SIZE } from '@/constants/shared'
import { cn } from '@/utils/shared'

/**
 * The shop's parcels, one step at a time (specs/038): waiting, being prepared, shipped. Oldest first,
 * because this is a queue - the order the server already keeps it in.
 *
 * <p>
 * The step is in the address (`?status=`), so a reload or a shared link lands on the same list.
 * </p>
 */
export function AdminOrdersPage() {
  const { t, i18n } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const asked = params.get('status')
  const status: QueueState = QUEUE_STATES.includes(asked as QueueState) ? (asked as QueueState) : 'Paid'
  const page = Number(params.get('page') ?? '1') || 1
  const queue = useFulfilmentQueue(status, page, PAGE_SIZE)

  return (
    <section className="grid gap-6">
      <PageTitle title={t('queue.title')} subtitle={t('queue.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex gap-1 justify-self-start rounded-full p-1 ring-1">
        {QUEUE_STATES.map((state) => (
          <button
            key={state}
            type="button"
            role="tab"
            aria-selected={state === status}
            onClick={() => setParams({ status: state })}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              state === status ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`queue.state.${state}`)}
          </button>
        ))}
      </div>

      {queue.isError ? (
        <ErrorMessage>{t('queue.loadFailed')}</ErrorMessage>
      ) : queue.isPending || !queue.data ? (
        <LoadingRows />
      ) : queue.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('queue.none')}</p>
      ) : (
        <>
          <ul className="grid gap-3">
            {queue.data.items.map((order) => (
              <li key={order.orderId}>
                <Link
                  to={`/admin/orders/${order.orderId}`}
                  className="bg-card ring-border/60 hover:ring-primary/60 grid gap-1 rounded-3xl p-4 ring-1 transition-all"
                >
                  <span className="font-medium">
                    {t('queue.placedAt', { at: new Date(order.createdAt).toLocaleString(i18n.language) })}
                  </span>
                  <span className="text-sm">
                    {t('queue.items', { count: order.itemCount })} ·{' '}
                    <Price value={order.totalAmount} currency={order.currency} className="font-semibold" />
                  </span>
                  {order.shipmentCount > 1 && (
                    <span className="text-muted-foreground text-xs">
                      {t('queue.parcels', { shipped: order.shipmentsShipped, count: order.shipmentCount })}
                    </span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={queue.data.totalCount}
            onChange={(next) => setParams({ status, page: String(next) })}
          />
        </>
      )}
    </section>
  )
}
