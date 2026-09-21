import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../api/http'
import { getCart, lineProblem, removeLine, setQuantity, type Cart } from '../api/cart'
import { money } from '../api/catalog'

// The cart (#37). It lives in the Cart service, keyed by the signed-in customer, so it survives
// sign-out and sign-in and follows the customer between devices. Every change is sent straight away
// and the cart is re-read, because the names and prices on it come from Catalog, not from this page.
export function CartPage() {
  const [cart, setCart] = useState<Cart | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const reload = useCallback(
    () =>
      getCart()
        .then((c) => {
          setCart(c)
          setError(null)
        })
        .catch(() => setError('The cart could not be loaded.')),
    [],
  )

  useEffect(() => {
    void reload()
  }, [reload])

  async function change(action: () => Promise<void>) {
    setBusy(true)
    try {
      await action()
      await reload()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'That change could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  if (!cart) return error ? <p className="error">{error}</p> : <p className="muted">Loading…</p>

  if (cart.lines.length === 0) {
    return (
      <section>
        <h1>Your cart</h1>
        <p>
          Your cart is empty. <Link to="/">Browse the shop</Link>.
        </p>
      </section>
    )
  }

  return (
    <section>
      <h1>Your cart</h1>
      {error && <p className="error">{error}</p>}
      {!cart.pricesAvailable && (
        <p className="error">Prices could not be checked just now. Your items are safe; try again in a moment.</p>
      )}

      <table className="lines">
        <thead>
          <tr>
            <th>Product</th>
            <th>Price</th>
            <th>Quantity</th>
            <th>Total</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {cart.lines.map((line) => {
            const problem = lineProblem(line.status)
            return (
              <tr key={line.productId}>
                <td>
                  <Link to={`/products/${line.productId}`}>{line.name ?? 'Unknown product'}</Link>
                  {problem && <div className="error small">{problem}</div>}
                </td>
                <td>{line.unitPrice === null ? '-' : money(line.unitPrice)}</td>
                <td>
                  <input
                    key={line.quantity} // re-read from the server after every change
                    type="number"
                    min={1}
                    className="qty"
                    aria-label={`Quantity of ${line.name ?? 'this product'}`}
                    defaultValue={line.quantity}
                    disabled={busy}
                    onBlur={(e) => {
                      const next = Number(e.target.value)
                      if (Number.isInteger(next) && next > 0 && next !== line.quantity) {
                        void change(() => setQuantity(line.productId, next))
                      } else {
                        e.target.value = String(line.quantity)
                      }
                    }}
                  />
                </td>
                <td>{line.lineTotal === null ? '-' : money(line.lineTotal)}</td>
                <td>
                  <button className="link" disabled={busy} onClick={() => void change(() => removeLine(line.productId))}>
                    Remove
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      <p className="price large">
        {cart.estimatedTotal === null ? 'Total unavailable' : `Estimated ${money(cart.estimatedTotal)}`}
      </p>
      <p className="muted small">
        An estimate from today's prices, before shipping and tax. The final amount is fixed at checkout.
      </p>
      {!cart.canCheckOut && <p className="error">Remove or fix the items marked above before checking out.</p>}
    </section>
  )
}
