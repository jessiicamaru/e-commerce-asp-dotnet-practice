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

/** What a category's products are compared by (specs/159): a text, or a choice of translated options. */
export interface Specification {
  id: string
  categoryId: string
  code: string
  name: string
  kind: 'Text' | 'Choice'
  position: number
  options: SpecificationOption[]
  /** The language `name` is in, after the fallback. */
  language?: string
}

export interface SpecificationOption {
  id: string
  code: string
  value: string
  language?: string
}

export interface NewSpecification {
  code: string
  name: string
  kind: 'Text' | 'Choice'
  options?: { code: string; value: string }[]
}
