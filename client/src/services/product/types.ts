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
  /** Null when there is none. Versioned, so it changes whenever the image does (specs/019). */
  imageUrl: string | null
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
