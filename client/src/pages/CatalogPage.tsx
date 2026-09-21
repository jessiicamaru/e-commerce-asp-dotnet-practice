import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { listCategories, listProducts, money, type Category, type Page, type Product, type SortBy } from '../api/catalog'

const PAGE_SIZE = 12

// The listing (#36), driven by the URL so a search can be shared, bookmarked and survives a reload.
export function CatalogPage() {
  const [params, setParams] = useSearchParams()
  const searchTerm = params.get('q') ?? ''
  const categoryId = params.get('category') ?? ''
  const sortBy = (params.get('sort') as SortBy | null) ?? 'name_asc'
  const pageNumber = Number(params.get('page') ?? '1') || 1

  const [categories, setCategories] = useState<Category[]>([])
  const [page, setPage] = useState<Page<Product> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [draft, setDraft] = useState(searchTerm)

  useEffect(() => {
    listCategories().then(setCategories).catch(() => setCategories([]))
  }, [])

  useEffect(() => {
    listProducts({ pageNumber, pageSize: PAGE_SIZE, searchTerm, categoryId, sortBy })
      .then((p) => {
        setPage(p)
        setError(null)
      })
      .catch(() => setError('The catalogue could not be loaded. Is the backend running?'))
  }, [pageNumber, searchTerm, categoryId, sortBy])

  function update(changes: Record<string, string>) {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v) next.set(k, v)
      else next.delete(k)
    }
    if (!('page' in changes)) next.delete('page') // a new filter starts on page 1
    setParams(next)
  }

  function search(e: FormEvent) {
    e.preventDefault()
    update({ q: draft.trim() })
  }

  const categoryName = (id: string) => categories.find((c) => c.id === id)?.name

  return (
    <section>
      <h1>Shop</h1>

      <form className="filters" onSubmit={search}>
        <input type="search" placeholder="Search by name or SKU" value={draft} onChange={(e) => setDraft(e.target.value)} />
        <select value={categoryId} onChange={(e) => update({ category: e.target.value })} aria-label="Category">
          <option value="">All categories</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <select value={sortBy} onChange={(e) => update({ sort: e.target.value })} aria-label="Sort">
          <option value="name_asc">Name A–Z</option>
          <option value="name_desc">Name Z–A</option>
          <option value="price_asc">Price, low to high</option>
          <option value="price_desc">Price, high to low</option>
        </select>
        <button>Search</button>
      </form>

      {error && <p className="error">{error}</p>}
      {page && (
        <>
          <p className="muted">
            {page.totalCount === 0
              ? 'Nothing matches.'
              : `${page.totalCount} product${page.totalCount === 1 ? '' : 's'}${searchTerm ? ` for “${searchTerm}”` : ''}`}
          </p>
          <ul className="grid">
            {page.items.map((p) => (
              <li key={p.id} className="card">
                <Link to={`/products/${p.id}`} className="card-link">
                  {/* No product has an image yet - see #45. */}
                  <div className="image-placeholder" aria-hidden="true">
                    {p.name.charAt(0)}
                  </div>
                  <strong>{p.name}</strong>
                </Link>
                <span className="muted small">{categoryName(p.categoryId) ?? ''}</span>
                <span className="price">{money(p.price)}</span>
                <Availability value={p.availability} />
              </li>
            ))}
          </ul>
          {page.totalPages > 1 && (
            <nav className="pager">
              <button disabled={!page.hasPreviousPage} onClick={() => update({ page: String(pageNumber - 1) })}>
                Previous
              </button>
              <span className="muted">
                Page {page.pageNumber} of {page.totalPages}
              </span>
              <button disabled={!page.hasNextPage} onClick={() => update({ page: String(pageNumber + 1) })}>
                Next
              </button>
            </nav>
          )}
        </>
      )}
    </section>
  )
}

/** In stock / out of stock, never a number: Catalog only ever knows that much (specs/004). */
export function Availability({ value }: { value: string }) {
  return value === 'InStock' ? <span className="in-stock">In stock</span> : <span className="out-of-stock">Out of stock</span>
}
