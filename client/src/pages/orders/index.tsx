import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Pager } from '@/components/shared/pager'
import { Card, CardContent } from '@/components/ui/card'
import { PAGE_SIZE } from '@/constants/shared'
import { useMyOrders } from '@/hooks/order'
import { describeOrderStatus } from '@/utils/order'
import { money } from '@/utils/shared'

/**
 * The customer's orders, newest first (#39). Order scopes the list to the caller's token; there is no
 * user id anywhere in the request.
 */
export function OrdersPage() {
  const { t, i18n } = useTranslation('orders')
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
            <Card>
              <CardContent className="flex flex-col gap-1 p-4">
                <Link to={`/orders/${order.orderId}`} className="font-medium hover:underline">
                  {new Date(order.createdAt).toLocaleString(i18n.language)}
                </Link>
                <span className="text-sm">
                  {t('itemCount', { count: order.itemCount })} ·{' '}
                  <span className="font-semibold">{money(order.totalAmount, order.currency)}</span>
                </span>
                <span className="text-muted-foreground text-xs">
                  {describeOrderStatus(t, order.status, order.failureReason)}
                </span>
              </CardContent>
            </Card>
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
