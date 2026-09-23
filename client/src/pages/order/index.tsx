import { useTranslation } from 'react-i18next'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { CancelOrder } from '@/components/order/cancel-order'
import { OrderLines } from '@/components/order/order-lines'
import { OrderShipments } from '@/components/order/order-shipments'
import { ReceiveParcel } from '@/components/order/receive-parcel'
import { OrderStatus } from '@/components/order/order-status'
import { OrderTotals } from '@/components/order/order-totals'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ORDER_STATUS, isSettling } from '@/constants/order'
import { useCancelOrder, useOrder, useReceiveParcel } from '@/hooks/order'
import { allDelivered } from '@/utils/order/delivery'
import { customerCanCancel } from '@/utils/order/cancel'
import { describeAddress } from '@/utils/address'

/**
 * One order (#38, #39). Right after checkout it is still `Submitted` while the saga reserves the stock
 * and takes payment, so the hook polls until it settles and the page says what is happening.
 */
export function OrderPage() {
  const { t, i18n } = useTranslation('orders')
  const { id = '' } = useParams()
  const justPlaced = (useLocation().state as { justPlaced?: boolean } | null)?.justPlaced ?? false
  const { data: order, isPending, error, isRefetching } = useOrder(id)
  const cancel = useCancelOrder(id)
  const receive = useReceiveParcel(id)

  if (error) {
    // Another customer's order is simply not found (#39): the page cannot tell the two apart either.
    const status = ApiError.from(error).status
    return <ErrorMessage>{status === 404 ? t('order.notFound') : t('order.loadFailed')}</ErrorMessage>
  }

  if (isPending || !order) {
    return <LoadingRows rows={2} />
  }

  const settling = isSettling(order.status)
  // The hook stops polling at its limit; when it has and the order is still Submitted, say so.
  const gaveUp = settling && !isRefetching

  return (
    <section>
      <p className="mb-4">
        <Link to="/orders" className="text-sm underline">
          {t('order.back')}
        </Link>
      </p>
      <h1 className="text-2xl font-bold">
        {justPlaced && !settling && order.status !== ORDER_STATUS.failed ? t('order.thanks') : t('order.title')}
      </h1>
      <p className="text-muted-foreground mb-4 text-xs">
        {t('order.placedAt', { id: order.orderId, at: new Date(order.createdAt).toLocaleString(i18n.language) })}
      </p>

      <OrderStatus
        status={order.status}
        failureReason={order.failureReason}
        showSpinner={settling && !gaveUp}
        overrideMessage={
          gaveUp
            ? t('order.taking')
            : order.status === ORDER_STATUS.cancelled
              ? t(order.cancelledBy === 'Customer' ? 'cancel.byYou' : 'cancel.byShop')
              : allDelivered(order)
                ? t('status.delivered')
                : undefined
        }
      />

      {customerCanCancel(order) && (
        <div className="mt-3">
          <CancelOrder cancel={cancel} />
        </div>
      )}

      {order.status === ORDER_STATUS.failed && (
        <p className="mt-3">
          <Link to="/cart" className="underline">
            {t('order.backToCart')}
          </Link>
        </p>
      )}
      {order.trackingReference && (
        <p className="mt-3 text-sm">{t('order.tracking', { reference: order.trackingReference })}</p>
      )}
      {/* One parcel: the list below is not drawn, so its "received" lives here (specs/040). */}
      {order.shipments?.length === 1 && (
        <div className="mt-3">
          <ReceiveParcel shipment={order.shipments[0]} receive={receive} />
        </div>
      )}

      {/* A cancelled order has no parcels to follow - nothing will be sent. */}
      {order.shipments && order.status !== ORDER_STATUS.cancelled && (
        <div className="mt-6">
          <OrderShipments shipments={order.shipments} receive={receive} />
        </div>
      )}

      <div className="mt-4">
        <OrderLines items={order.items} currency={order.currency} />
      </div>
      <OrderTotals totals={order} shippingName={order.shippingOption?.name} />

      {order.shippingAddress && (
        <p className="text-sm">
          <span className="font-medium">{t('order.deliveringTo')}</span> {order.shippingAddress.recipientName},{' '}
          {describeAddress(order.shippingAddress)}
        </p>
      )}
    </section>
  )
}
