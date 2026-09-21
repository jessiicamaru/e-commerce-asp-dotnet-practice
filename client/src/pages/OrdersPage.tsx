import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { money } from '../api/catalog'
import { describeStatus, listMyOrders, type OrderPage } from '../api/orders'

const PAGE_SIZE = 10

// The customer's orders, newest first (#39). Order scopes the list to the caller's token; there is no
// user id anywhere in the request.
export function OrdersPage() {
  const [params, setParams] = useSearchParams()
  const page = Number(params.get('page') ?? '1') || 1
  const [result, setResult] = useState<OrderPage | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    listMyOrders(page, PAGE_SIZE)
      .then((r) => {
        setResult(r)
        setError(null)
      })
      .catch(() => setError('Your orders could not be loaded.'))
  }, [page])

  if (!result) return error ? <p className="error">{error}</p> : <p className="muted">Loading…</p>

  const pages = Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE))

  return (
    <section>
      <h1>Your orders</h1>
      {error && <p className="error">{error}</p>}
      {result.totalCount === 0 ? (
        <p>
          You have not ordered anything yet. <Link to="/">Browse the shop</Link>.
        </p>
      ) : (
        <>
          <ul className="addresses">
            {result.items.map((o) => (
              <li key={o.orderId} className="card">
                <Link to={`/orders/${o.orderId}`}>
                  <strong>{new Date(o.createdAt).toLocaleString()}</strong>
                </Link>
                <span>
                  {o.itemCount} item{o.itemCount === 1 ? '' : 's'} · <span className="price">{money(o.totalAmount)}</span>
                </span>
                <span className={`small status-${o.status.toLowerCase()}`}>{describeStatus(o.status, o.failureReason)}</span>
              </li>
            ))}
          </ul>
          {pages > 1 && (
            <nav className="pager">
              <button disabled={page <= 1} onClick={() => setParams({ page: String(page - 1) })}>
                Newer
              </button>
              <span className="muted">
                Page {page} of {pages}
              </span>
              <button disabled={page >= pages} onClick={() => setParams({ page: String(page + 1) })}>
                Older
              </button>
            </nav>
          )}
        </>
      )}
    </section>
  )
}
