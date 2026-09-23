import { http } from '@/config/axios'
import type { Stock as StockModel } from './types'

/**
 * Inventory's stock, keyed by **variant** id (specs/020) — the sellable unit.
 *
 * Reading is anonymous: a real number is public, unlike Catalog's `availability`, which is a read
 * model fed by messages and says only "InStock" or not (specs/004).
 *
 * Writing is a seller's own product or an administrator's anything (specs/031). Somebody else's is
 * a **404**, and the storefront must repeat that rather than translate it into "you are not
 * allowed" — which would undo the reason it is a 404.
 */
export class Stock {
  static async get(variantId: string): Promise<StockModel> {
    const { data } = await http.get<StockModel>(`/stock/${variantId}`, { anonymous: true })
    return data
  }

  static async setOnHand(variantId: string, quantityOnHand: number): Promise<StockModel> {
    const { data } = await http.put<StockModel>(`/stock/${variantId}`, { quantityOnHand })
    return data
  }
}
