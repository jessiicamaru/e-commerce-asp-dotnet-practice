import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ApiError } from '../api/http'
import { describe } from '../api/addresses'
import { money } from '../api/catalog'
import { describeStatus, getOrder, isSettling, type Order, type Totals as TotalParts } from '../api/orders'

const POLL_MS = 1000
/** The saga settles in about two seconds; past this the page stops asking and says so. */
const POLL_LIMIT = 30

// One order (#38, #39). Right after checkout the order is still Submitted while the saga reserves the
// stock and takes payment, so the page polls until it settles, saying what is happening meanwhile.
export function OrderPage() {
  const { id = '' } = useParams()
  const justPlaced = (useLocation().state as { justPlaced?: boolean } | null)?.justPlaced ?? false
  const [loaded, setLoaded] = useState<{ id: string; order?: Order; error?: string; polls: number } | null>(null)

  useEffect(() => {
    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined

    const poll = (polls: number) => {
      getOrder(id)
        .then((order) => {
          if (cancelled) return
          setLoaded({ id, order, polls })
          if (isSettling(order.status) && polls < POLL_LIMIT) timer = setTimeout(() => poll(polls + 1), POLL_MS)
        })
        .catch((e: unknown) => {
          if (cancelled) return
          // Another customer's order is simply not found (#39): the page cannot tell the two apart either.
          setLoaded({ id, polls, error: e instanceof ApiError && e.status === 404 ? 'Order not found.' : 'The order could not be loaded.' })
        })
    }
    poll(0)

    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [id])

  const current = loaded?.id === id ? loaded : null
  if (current?.error) return <p className="error">{current.error}</p>
  const order = current?.order
  if (!order) return <p className="muted">Loading…</p>

  const settling = isSettling(order.status)
  const gaveUp = settling && current.polls >= POLL_LIMIT

  return (
    <section>
      <p>
        <Link to="/orders">← Your orders</Link>
      </p>
      <h1>{justPlaced && !settling && order.status !== 'Failed' ? 'Thank you for your order' : 'Order'}</h1>
      <p className="muted small">
        {order.orderId} · placed {new Date(order.createdAt).toLocaleString()}
      </p>

      <p className={`status status-${order.status.toLowerCase()}`} role="status">
        {settling && !gaveUp && <span className="spinner" aria-hidden="true" />}
        {gaveUp
          ? 'This is taking longer than usual. Your order is safe; check back in a minute.'
          : describeStatus(order.status, order.failureReason)}
      </p>
      {order.status === 'Failed' && (
        <p>
          <Link to="/cart">Back to your cart</Link>
        </p>
      )}
      {order.trackingReference && <p>Tracking reference: {order.trackingReference}</p>}

      <table className="lines">
        <tbody>
          {order.items.map((i) => (
            <tr key={i.productId}>
              <td>{i.productName}</td>
              <td>
                {i.quantity} × {money(i.unitPrice)}
              </td>
              <td>{money(i.totalPrice)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <Totals totals={order} shippingName={order.shippingOption?.name} />

      {order.shippingAddress && (
        <p>
          <strong>Delivering to</strong> {order.shippingAddress.recipientName}, {describe(order.shippingAddress)}
        </p>
      )}
    </section>
  )
}

/** The named parts of a total, as stored on the order (or quoted for it). */
export function Totals({ totals, shippingName }: { totals: TotalParts; shippingName?: string }) {
  const rate = totals.taxRate === null ? '' : ` (${+(totals.taxRate * 100).toFixed(2)}%)`
  return (
    <dl className="totals">
      {totals.subtotal !== null && (
        <>
          <dt>Items</dt>
          <dd>{money(totals.subtotal)}</dd>
        </>
      )}
      {totals.shippingPrice !== null && (
        <>
          <dt>Delivery{shippingName ? ` · ${shippingName}` : ''}</dt>
          <dd>{money(totals.shippingPrice)}</dd>
        </>
      )}
      {totals.taxTotal !== null && (
        <>
          <dt>Tax{rate}</dt>
          <dd>{money(totals.taxTotal)}</dd>
        </>
      )}
      {!!totals.discountTotal && (
        <>
          <dt>Discount</dt>
          <dd>-{money(totals.discountTotal)}</dd>
        </>
      )}
      <dt className="grand">Total</dt>
      <dd className="grand">{money(totals.totalAmount)}</dd>
    </dl>
  )
}
