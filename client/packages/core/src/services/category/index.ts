// The model types live in ./types, imported from there: this file's export is the class, and a
// class and an interface cannot share a name.
import { http } from '@ecommerce/core/config/axios'
import type { Category as CategoryModel, NewSpecification, Specification } from './types'

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

  /** What applies to products filed under a category - its department's, then its own (specs/159). */
  static async specifications(categoryId: string): Promise<Specification[]> {
    const { data } = await http.get<Specification[]>(`/categories/${categoryId}/specifications`, { anonymous: true })
    return data
  }

  /** Every specification in one language - for the administrators' page, which shows two side by side. */
  static async specificationsIn(categoryId: string, language: string): Promise<Specification[]> {
    const { data } = await http.get<Specification[]>(`/categories/${categoryId}/specifications?lang=${language}`, { anonymous: true })
    return data
  }

  static async createSpecification(categoryId: string, specification: NewSpecification): Promise<Specification> {
    const { data } = await http.post<Specification>(`/categories/${categoryId}/specifications`, specification)
    return data
  }

  static async translateSpecification(categoryId: string, id: string, language: string, name: string): Promise<Specification> {
    const { data } = await http.put<Specification>(`/categories/${categoryId}/specifications/${id}/translations/${language}`, { name })
    return data
  }

  /** Refused with 409 while a product has a value for it. */
  static async removeSpecification(categoryId: string, id: string): Promise<void> {
    await http.delete(`/categories/${categoryId}/specifications/${id}`)
  }

  static async addOption(categoryId: string, specificationId: string, code: string, value: string): Promise<Specification> {
    const { data } = await http.post<Specification>(`/categories/${categoryId}/specifications/${specificationId}/options`, { code, value })
    return data
  }

  static async translateOption(
    categoryId: string,
    specificationId: string,
    optionId: string,
    language: string,
    value: string,
  ): Promise<Specification> {
    const { data } = await http.put<Specification>(
      `/categories/${categoryId}/specifications/${specificationId}/options/${optionId}/translations/${language}`,
      { value },
    )
    return data
  }

  /** Refused with 409 while a product holds it. */
  static async removeOption(categoryId: string, specificationId: string, optionId: string): Promise<void> {
    await http.delete(`/categories/${categoryId}/specifications/${specificationId}/options/${optionId}`)
  }

  /** Refused with 409 while products are filed under it (specs/024). */
  static async remove(id: string): Promise<void> {
    await http.delete(`/categories/${id}`)
  }
}
