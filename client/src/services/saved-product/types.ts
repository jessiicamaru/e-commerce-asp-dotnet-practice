import type { Product } from '@/services/product/types'

/**
 * A product the shopper saved for later (specs/075), as the listing reads it NOW - in their language and
 * currency. `available` is whether it can be bought: on sale, active and in stock. A product taken down since
 * stays in the list with `available: false` rather than vanishing from something the shopper chose.
 */
export interface SavedItem {
  product: Product
  savedAt: string
  available: boolean
}

export interface SavedPage {
  items: SavedItem[]
  pageNumber: number
  totalPages: number
  totalCount: number
}
