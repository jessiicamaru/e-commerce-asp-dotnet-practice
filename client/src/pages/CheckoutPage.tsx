import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/http'
import { describe, listAddresses, type Address } from '../api/addresses'
import { money } from '../api/catalog'
import { getQuote, listShippingOptions, placeOrder, type Quote, type ShippingOption } from '../api/orders'
import { Totals } from './OrderPage'

// Checkout (#38). The customer picks where and how; everything else comes from the server. The
// breakdown shown is Order's own quote, computed by the same code that then prices the order, so the
// total here is the total charged unless the cart or a price changes in between.
export function CheckoutPage() {
  const navigate = useNavigate()
  const [addresses, setAddresses] = useState<Address[] | null>(null)
  const [options, setOptions] = useState<ShippingOption[]>([])
  const [addressId, setAddressId] = useState<string | null>(null)
  const [shippingOption, setShippingOption] = useState<string | null>(null)
  const [quoted, setQuoted] = useState<{ key: string; quote?: Quote; error?: string } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [placing, setPlacing] = useState(false)

  useEffect(() => {
    Promise.all([listAddresses(), listShippingOptions()])
      .then(([a, o]) => {
        setAddresses(a)
        setOptions(o)
        setAddressId((a.find((x) => x.isDefault) ?? a[0])?.id ?? null)
        setShippingOption(o[0]?.code ?? null)
      })
      .catch(() => setError('Checkout could not be loaded.'))
  }, [])

  const key = `${addressId}|${shippingOption}`

  useEffect(() => {
    if (!addressId || !shippingOption) return
    const k = `${addressId}|${shippingOption}`
    getQuote({ addressId, shippingOption })
      .then((quote) => setQuoted({ key: k, quote }))
      .catch((e: unknown) =>
        setQuoted({
          key: k,
          error:
            e instanceof ApiError && e.status === 409
              ? e.problem.detail ?? 'This cart cannot be checked out.'
              : e instanceof ApiError && e.status === 503
                ? 'A service needed to price your order is unavailable. Try again in a moment.'
                : 'Your order could not be priced.',
        }),
      )
  }, [addressId, shippingOption])

  async function place() {
    if (!addressId || !shippingOption) return
    setPlacing(true)
    setError(null)
    try {
      const order = await placeOrder({ addressId, shippingOption })
      navigate(`/orders/${order.orderId}`, { state: { justPlaced: true } })
    } catch (e) {
      setError(e instanceof ApiError ? (e.problem.detail ?? e.message) : 'The order could not be placed.')
      setPlacing(false)
    }
  }

  if (!addresses) return error ? <p className="error">{error}</p> : <p className="muted">Loading…</p>

  if (addresses.length === 0) {
    return (
      <section>
        <h1>Checkout</h1>
        <p>
          You need a delivery address first. <Link to="/addresses">Add one</Link>.
        </p>
      </section>
    )
  }

  const current = quoted?.key === key ? quoted : null

  return (
    <section className="checkout">
      <h1>Checkout</h1>

      <fieldset>
        <legend>Deliver to</legend>
        {addresses.map((a) => (
          <label key={a.id} className="choice">
            <input type="radio" name="address" checked={addressId === a.id} onChange={() => setAddressId(a.id)} />
            <span>
              <strong>{a.recipientName}</strong> {a.isDefault && <span className="badge">Default</span>}
              <br />
              <span className="muted small">{describe(a)}</span>
            </span>
          </label>
        ))}
        <Link to="/addresses" className="small">
          Manage addresses
        </Link>
      </fieldset>

      <fieldset>
        <legend>Delivery</legend>
        {options.map((o) => (
          <label key={o.code} className="choice">
            <input
              type="radio"
              name="shipping"
              checked={shippingOption === o.code}
              onChange={() => setShippingOption(o.code)}
            />
            <span>
              {o.name} · {money(o.price)}
            </span>
          </label>
        ))}
      </fieldset>

      {!current ? (
        <p className="muted">Pricing your order…</p>
      ) : current.error ? (
        <p className="error">
          {current.error} <Link to="/cart">Back to the cart</Link>
        </p>
      ) : (
        current.quote && (
          <>
            <table className="lines">
              <tbody>
                {current.quote.items.map((i) => (
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
            <Totals totals={current.quote} shippingName={current.quote.shippingOption.name} />
            {error && <p className="error" role="alert">{error}</p>}
            <button disabled={placing} onClick={() => void place()}>
              {placing ? 'Placing your order…' : `Place order · ${money(current.quote.totalAmount)}`}
            </button>
          </>
        )
      )}
    </section>
  )
}
