import { useTranslation } from 'react-i18next'
import { useSearchParams, useLocation } from 'react-router-dom'
import { OrderRow } from '@/components/order/order-row'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
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
  const { t } = useTranslation('admin')
  const location = useLocation()
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
                {/* Reference, products and status (specs/132); where it was opened from rides along (specs/129). */}
                <OrderRow
                  to={`/admin/orders/${order.orderId}`}
                  state={{ from: `${location.pathname}${location.search}` }}
                  orderId={order.orderId}
                  lines={order.lines}
                  lineCount={order.itemCount}
                  createdAt={order.createdAt}
                  status={order.status}
                  shipped={order.shipmentsShipped}
                  parcels={order.shipmentCount}
                  amount={order.totalAmount}
                  currency={order.currency}
                />
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
