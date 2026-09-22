// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@/config/axios'
import type { Category as CategoryModel } from './types'

export class Category {
  static async list(): Promise<CategoryModel[]> {
    const { data } = await http.get<CategoryModel[]>('/categories', { anonymous: true })
    return data
  }
}
