export interface Category {
  id: string
  name: string
  description: string | null
  slug: string
  parentCategoryId: string | null
  isActive: boolean
  /** The language the name above is in, after the fallback; empty when none was asked for (specs/026). */
  language?: string
}
