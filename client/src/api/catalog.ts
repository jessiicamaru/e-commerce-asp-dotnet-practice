import { api } from './http'

// Catalog, as the storefront sees it (#36). Everything here is anonymous - browsing needs no account.

export interface Product {
  id: string
  name: string
  description: string | null
  price: number
  /** "InStock" / "OutOfStock" - a read model fed by Inventory, never a count (specs/004). */
  availability: 'InStock' | 'OutOfStock' | string
  sku: string
  categoryId: string
  isActive: boolean
  /** Null when there is none. Versioned: it changes whenever the image does, so it is safe to cache (specs/019). */
  imageUrl: string | null
}

export interface Category {
  id: string
  name: string
  description: string | null
  slug: string
  parentCategoryId: string | null
  isActive: boolean
}

export interface Page<T> {
  items: T[]
  pageNumber: number
  totalPages: number
  totalCount: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export type SortBy = 'name_asc' | 'name_desc' | 'price_asc' | 'price_desc'

export interface ProductQuery {
  pageNumber?: number
  pageSize?: number
  categoryId?: string
  searchTerm?: string
  sortBy?: SortBy
}

export function listProducts(q: ProductQuery): Promise<Page<Product>> {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(q)) {
    if (value !== undefined && value !== '') params.set(key, String(value))
  }
  return api<Page<Product>>(`/products?${params}`, { anonymous: true })
}

export const getProduct = (id: string) => api<Product>(`/products/${id}`, { anonymous: true })

export const listCategories = () => api<Category[]>('/categories', { anonymous: true })

/** One currency, as the backend assumes; formatted, never computed with. */
export const money = (value: number) =>
  value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
