import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { ErrorMessage, LoadingRows } from '@ecommerce/core/components/query-state'
import { Pager } from '@/components/shared/pager'
import { OrderRow } from '@/components/order/order-row'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { useMyOrders } from '@ecommerce/core/hooks/order'
import { describeOrderStatus } from '@ecommerce/core/utils/order'

/**
 * The customer's orders, newest first (#39). Order scopes the list to the caller's token; there is no
 * user id anywhere in the request.
 */
export function OrdersPage() {
  const { t } = useTranslation('orders')
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const { data: result, isPending, isError } = useMyOrders(page, PAGE_SIZE)

  if (isError) {
    return <ErrorMessage>{t('loadFailed')}</ErrorMessage>
  }

  if (isPending || !result) {
    return <LoadingRows />
  }

  if (result.totalCount === 0) {
    return (
      <section>
        <h1 className="mb-4 text-2xl font-bold">{t('title')}</h1>
        <p>
          {t('none')}{' '}
          <Link to="/" className="underline">
            {t('browse')}
          </Link>
          .
        </p>
      </section>
    )
  }

  return (
    <section>
      <h1 className="mb-4 text-2xl font-bold">{t('title')}</h1>
      <ul className="grid gap-3">
        {result.items.map((order) => (
          <li key={order.orderId}>
            {/* One row per order, the whole of it the link (specs/132): reference, products, status chip. Partly sent
                says so in the chip (specs/035). */}
            <OrderRow
              to={`/orders/${order.orderId}`}
              orderId={order.orderId}
              lines={order.lines}
              lineCount={order.itemCount}
              createdAt={order.createdAt}
              status={order.status}
              shipped={order.shipmentsShipped}
              parcels={order.shipmentCount}
              amount={order.totalAmount}
              currency={order.currency}
              detail={
                order.status === 'Failed' ? (
                  <span className="text-muted-foreground text-xs">{describeOrderStatus(t, order.status, order.failureReason)}</span>
                ) : undefined
              }
            />
          </li>
        ))}
      </ul>
      <Pager
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={result.totalCount}
        onChange={(next) => setParams({ page: String(next) })}
      />
    </section>
  )
}
