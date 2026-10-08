// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@ecommerce/core/config/axios'
import type { Category as CategoryModel } from './types'

export class Category {
  static async list(): Promise<CategoryModel[]> {
    const { data } = await http.get<CategoryModel[]>('/categories', { anonymous: true })
    return data
  }

  /** Every category as it reads in one language - `language` on each says whether it was translated (specs/026). */
  static async listIn(language: string): Promise<CategoryModel[]> {
    const { data } = await http.get<CategoryModel[]>(`/categories?lang=${language}`, { anonymous: true })
    return data
  }

  /**
   * Administrators (specs/097): a new category; the slug is its address and never changes. Under a department, or a
   * department itself when `parentCategoryId` is null (specs/158).
   */
  static async create(
    name: string,
    slug: string,
    description: string | null,
    parentCategoryId: string | null = null,
  ): Promise<CategoryModel> {
    const { data } = await http.post<CategoryModel>('/categories', { name, slug, description, parentCategoryId })
    return data
  }

  /** Under another department, or - null - a department of its own (specs/158). Its own endpoint: a rename never moves. */
  static async move(id: string, parentCategoryId: string | null): Promise<CategoryModel> {
    const { data } = await http.put<CategoryModel>(`/categories/${id}/parent`, { parentCategoryId })
    return data
  }

  /** The category's own - default-language - name and description. */
  static async update(id: string, name: string, description: string | null): Promise<CategoryModel> {
    const { data } = await http.put<CategoryModel>(`/categories/${id}`, { name, description })
    return data
  }

  static async translate(id: string, language: string, name: string, description: string | null): Promise<CategoryModel> {
    const { data } = await http.put<CategoryModel>(`/categories/${id}/translations/${language}`, { name, description })
    return data
  }

  static async removeTranslation(id: string, language: string): Promise<void> {
    await http.delete(`/categories/${id}/translations/${language}`)
  }

  /** Refused with 409 while products are filed under it (specs/024). */
  static async remove(id: string): Promise<void> {
    await http.delete(`/categories/${id}`)
  }
}
