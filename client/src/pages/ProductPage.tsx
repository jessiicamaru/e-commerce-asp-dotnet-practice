import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../api/http'
import { getProduct, money, type Product } from '../api/catalog'
import { Availability } from './CatalogPage'

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
        <div className="image-placeholder large" aria-hidden="true">
          {product.name.charAt(0)}
        </div>
        <div>
          <h1>{product.name}</h1>
          <p className="price large">{money(product.price)}</p>
          <p className="muted small">Price excludes tax, which is added at checkout for your delivery country.</p>
          <Availability value={product.availability} />
          {product.description && <p>{product.description}</p>}
          <p className="muted small">SKU {product.sku}</p>
        </div>
      </div>
    </section>
  )
}
