import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { ApiError } from '../api/http'
import { getProduct, money, type Product } from '../api/catalog'
import { addToCart } from '../api/cart'
import { useAuth } from '../auth/useAuth'
import { Availability, ProductImage } from './CatalogPage'

export function ProductPage() {
  const { id = '' } = useParams()
  // The answer is stored WITH the id it answers, so a stale one is recognised during render rather than
  // cleared with a synchronous setState in the effect.
  const [loaded, setLoaded] = useState<{ id: string; product?: Product; error?: string } | null>(null)

  useEffect(() => {
    getProduct(id)
      .then((product) => setLoaded({ id, product }))
      .catch((e: unknown) =>
        setLoaded({
          id,
          error: e instanceof ApiError && e.status === 404 ? 'This product does not exist.' : 'The product could not be loaded.',
        }),
      )
  }, [id])

  const current = loaded?.id === id ? loaded : null
  const product = current?.product
  const error = current?.error

  if (error) return <p className="error">{error}</p>
  if (!product) return <p className="muted">Loading…</p>

  return (
    <section className="product">
      <p>
        <Link to="/">← Back to the shop</Link>
      </p>
      <div className="product-layout">
        <ProductImage product={product} large />
        <div>
          <h1>{product.name}</h1>
          <p className="price large">{money(product.price)}</p>
          <p className="muted small">Price excludes tax, which is added at checkout for your delivery country.</p>
          <Availability value={product.availability} />
          <AddToCart productId={product.id} />
          {product.description && <p>{product.description}</p>}
          <p className="muted small">SKU {product.sku}</p>
        </div>
      </div>
    </section>
  )
}

// Adding needs an account: the cart is kept per customer by the Cart service, not in the browser.
function AddToCart({ productId }: { productId: string }) {
  const { user, restoring } = useAuth()
  const location = useLocation()
  const [quantity, setQuantity] = useState(1)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const [busy, setBusy] = useState(false)

  if (restoring) return null
  if (!user) {
    return (
      <p>
        <Link to="/sign-in" state={{ from: location.pathname }}>
          Sign in
        </Link>{' '}
        to add this to your cart.
      </p>
    )
  }

  async function add() {
    setBusy(true)
    setMessage(null)
    try {
      await addToCart(productId, quantity)
      setMessage({ ok: true, text: `Added ${quantity} to your cart.` })
    } catch (e) {
      setMessage({ ok: false, text: e instanceof ApiError ? e.message : 'It could not be added. Try again.' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="add-to-cart">
      <input
        type="number"
        min={1}
        className="qty"
        aria-label="Quantity"
        value={quantity}
        onChange={(e) => setQuantity(Math.max(1, Math.floor(Number(e.target.value)) || 1))}
      />
      <button disabled={busy} onClick={() => void add()}>
        {busy ? 'Adding…' : 'Add to cart'}
      </button>
      {message && (
        <span className={message.ok ? 'in-stock' : 'error'}>
          {message.text} {message.ok && <Link to="/cart">View cart</Link>}
        </span>
      )}
    </div>
  )
}
