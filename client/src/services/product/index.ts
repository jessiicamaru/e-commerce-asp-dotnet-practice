// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
import { CURRENCY_HEADER } from '@/config/money'
import { visitorId } from '@/utils/shared'
import type { NewProduct, Page, Product as ProductModel, ProductQuery } from './types'

/** Catalog's products. Browsing needs no account, so every call here is anonymous. */
export class Product {
  static async list(query: ProductQuery): Promise<Page<ProductModel>> {
    const params = new URLSearchParams()
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== '') {
        params.set(key, String(value))
      }
    }

    const { data } = await http.get<Page<ProductModel>>(`/products?${params}`, { anonymous: true })
    return data
  }

  /**
   * One product. `currency` overrides what the visitor is browsing in - needed by the seller's own
   * page, which shows every currency's price at once and therefore asks once per currency
   * (specs/022 gives a response one currency's prices, deliberately, because nothing converts).
   */
  static async get(id: string, currency?: string): Promise<ProductModel> {
    const { data } = await http.get<ProductModel>(`/products/${id}`, {
      anonymous: true,
      headers: currency ? { [CURRENCY_HEADER]: currency } : undefined,
    })
    return data
  }

  /**
   * The signed-in seller's own listings (specs/027).
   *
   * **It sends no seller id**, because there is nowhere to put one: the server reads whose shop
   * this is from the token. That is the whole point of the endpoint, and an id in the query string
   * would let one seller read another's page.
   */
  /**
   * Lists a product (specs/028).
   *
   * <p>
   * <b>`price` is in the shop's DEFAULT currency</b>, never in the one being browsed in.
   * `CreateProductCommand.Price` sets `product_variants.Price`, which specs/022 defines as the
   * default currency's amount, and the server validates it against that currency. Sending the
   * active currency's number here would list a 1,999-dong camera for somebody who typed 1999 meaning
   * dollars - and nothing would complain. The other currency's price is set afterwards, per
   * currency.
   * </p>
   * <p>
   * It carries no seller id either: whose listing this is comes from the token.
   * </p>
   */
  static async create(input: NewProduct): Promise<ProductModel> {
    const { data } = await http.post<ProductModel>('/products', input)
    return data
  }

  /**
   * What one shape of a product costs in ONE currency (specs/022). An upsert.
   *
   * The currency is in the path because it is part of what is being addressed, not a filter on it:
   * `.../prices/USD` is a different row from `.../prices/VND`, and nothing converts between them.
   */
  static async setPrice(productId: string, variantId: string, currency: string, amount: number): Promise<void> {
    await http.put(`/products/${productId}/variants/${variantId}/prices/${currency}`, { amount })
  }

  /** Stops selling this shape in this currency: it then reports NO price, not a converted one. */
  static async removePrice(productId: string, variantId: string, currency: string): Promise<void> {
    await http.delete(`/products/${productId}/variants/${variantId}/prices/${currency}`)
  }

  /**
   * One SHAPE's own photograph (specs/032). Removing it makes the variant fall back to the
   * product's, which is a real state rather than "no picture".
   */
  static async uploadVariantImage(productId: string, variantId: string, file: File): Promise<void> {
    const body = new FormData()
    body.append('file', file)
    await http.put(`/products/${productId}/variants/${variantId}/image`, body)
  }

  static async removeVariantImage(productId: string, variantId: string): Promise<void> {
    await http.delete(`/products/${productId}/variants/${variantId}/image`)
  }

  /** One multipart part named `file` - the name the server looks for (specs/019). */
  static async uploadImage(productId: string, file: File): Promise<void> {
    const body = new FormData()
    body.append('file', file)
    await http.put(`/products/${productId}/image`, body)
  }

  /**
   * Removes the listing for good (specs/024) - not the way to stop selling something.
   *
   * Orders are unaffected: each one froze what it bought, which is what freezing is for.
   */
  static async remove(productId: string): Promise<void> {
    await http.delete(`/products/${productId}`)
  }

  /**
   * The product page was opened (specs/047). Counted by the server only for a shopper, and once a day per viewer:
   * this browser's visitor id says who (specs/086). Always quiet.
   */
  static async recordView(productId: string): Promise<void> {
    await http.post(`/products/${productId}/view`, { viewer: visitorId() })
  }

  /** A seller sends their rejected product back to the moderators (specs/045). */
  static async resubmit(productId: string): Promise<ProductModel> {
    const { data } = await http.post<ProductModel>(`/products/${productId}/resubmit`)
    return data
  }

  static async mine(query: ProductQuery): Promise<Page<ProductModel>> {
    const params = new URLSearchParams()
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== '') {
        params.set(key, String(value))
      }
    }

    const { data } = await http.get<Page<ProductModel>>(`/products/mine?${params}`)
    return data
  }
}
