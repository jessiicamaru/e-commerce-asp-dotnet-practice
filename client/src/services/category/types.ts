export interface Category {
  id: string
  name: string
  description: string | null
  slug: string
  parentCategoryId: string | null
  isActive: boolean
}
