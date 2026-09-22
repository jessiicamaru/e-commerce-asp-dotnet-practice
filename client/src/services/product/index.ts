// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
import type { Page, Product as ProductModel, ProductQuery } from './types'

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

  static async get(id: string): Promise<ProductModel> {
    const { data } = await http.get<ProductModel>(`/products/${id}`, { anonymous: true })
    return data
  }
}
