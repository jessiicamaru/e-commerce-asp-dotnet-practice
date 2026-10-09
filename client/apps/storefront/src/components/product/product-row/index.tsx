import { ProductCard } from '@/components/product/product-card'
import type { Product } from '@ecommerce/core/services/product/types'

/**
 * A titled row of the listing's cards (specs/163): related products, recently viewed. Nothing at all when there is
 * nothing to show - an empty heading reads as something that failed to load.
 */
export function ProductRow({ title, products }: { title: string; products: Product[] }) {
  if (products.length === 0) return null
  const id = `row-${title.replace(/\s+/g, '-').toLowerCase()}`

  return (
    <section className="mt-10" aria-labelledby={id}>
      <h2 id={id} className="mb-4 text-xl font-bold tracking-tight">
        {title}
      </h2>
      <ul className="grid grid-cols-2 gap-3 sm:grid-cols-[repeat(auto-fill,minmax(13rem,1fr))] sm:gap-4">
        {products.map((product) => (
          <li key={product.id}>
            <ProductCard product={product} />
          </li>
        ))}
      </ul>
    </section>
  )
}
