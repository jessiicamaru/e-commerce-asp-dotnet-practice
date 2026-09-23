import type { Stock } from '@/services/stock/types'

/** Below this many available, a listing is "running low" - worth a seller's attention, not yet an alarm. */
export const LOW_STOCK = 3

export type StockLevel = 'unknown' | 'out' | 'low' | 'in'

/**
 * How worried to be about a number. `unknown` is a real state and not a zero: a freshly listed
 * variant has no stock row until Inventory hears about it through the broker (specs/031).
 */
export function stockLevel(available: number | null | undefined): StockLevel {
  if (available === null || available === undefined) return 'unknown'
  if (available <= 0) return 'out'
  if (available <= LOW_STOCK) return 'low'
  return 'in'
}

/**
 * The stock of a whole listing, summed over its variants. `known` says how many variants actually
 * answered - a sum over two of three is shown as a sum, but the page can say one is still missing.
 */
export function sumStock(rows: (Stock | null | undefined)[]) {
  const known = rows.filter((row): row is Stock => !!row)

  return {
    known: known.length,
    total: rows.length,
    onHand: known.reduce((sum, row) => sum + row.quantityOnHand, 0),
    reserved: known.reduce((sum, row) => sum + row.quantityReserved, 0),
    available: known.length === 0 ? null : known.reduce((sum, row) => sum + row.quantityAvailable, 0),
  }
}
