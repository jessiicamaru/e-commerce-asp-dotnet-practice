import { Link, useLocation, useParams } from 'react-router-dom'
import { ApiError } from '@/config/axios'
import { OrderLines } from '@/components/order/order-lines'
import { OrderStatus } from '@/components/order/order-status'
import { OrderTotals } from '@/components/order/order-totals'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { ORDER_STATUS, isSettling } from '@/constants/order'
import { useOrder } from '@/hooks/order'
import { describeAddress } from '@/utils/address'

/**
 * One order (#38, #39). Right after checkout it is still `Submitted` while the saga reserves the stock
 * and takes payment, so the hook polls until it settles and the page says what is happening.
 */
export function OrderPage() {
  const { id = '' } = useParams()
  const justPlaced = (useLocation().state as { justPlaced?: boolean } | null)?.justPlaced ?? false
  const { data: order, isPending, error, isRefetching } = useOrder(id)

  if (error) {
    // Another customer's order is simply not found (#39): the page cannot tell the two apart either.
    const status = ApiError.from(error).status
    return <ErrorMessage>{status === 404 ? 'Order not found.' : 'The order could not be loaded.'}</ErrorMessage>
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
          ← Your orders
        </Link>
      </p>
      <h1 className="text-2xl font-bold">
        {justPlaced && !settling && order.status !== ORDER_STATUS.failed ? 'Thank you for your order' : 'Order'}
      </h1>
      <p className="text-muted-foreground mb-4 text-xs">
        {order.orderId} · placed {new Date(order.createdAt).toLocaleString()}
      </p>

      <OrderStatus
        status={order.status}
        failureReason={order.failureReason}
        showSpinner={settling && !gaveUp}
        overrideMessage={gaveUp ? 'This is taking longer than usual. Your order is safe; check back in a minute.' : undefined}
      />

      {order.status === ORDER_STATUS.failed && (
        <p className="mt-3">
          <Link to="/cart" className="underline">
            Back to your cart
          </Link>
        </p>
      )}
      {order.trackingReference && <p className="mt-3 text-sm">Tracking reference: {order.trackingReference}</p>}

      <div className="mt-4">
        <OrderLines items={order.items} />
      </div>
      <OrderTotals totals={order} shippingName={order.shippingOption?.name} />

      {order.shippingAddress && (
        <p className="text-sm">
          <span className="font-medium">Delivering to</span> {order.shippingAddress.recipientName},{' '}
          {describeAddress(order.shippingAddress)}
        </p>
      )}
    </section>
  )
}
