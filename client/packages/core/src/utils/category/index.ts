import type { Category } from '@ecommerce/core/services/category/types'
import type { Choice } from '@ecommerce/core/utils/shared'

/**
 * Categories as a tree (specs/158, #363): departments, each with the categories under it - two levels, which the server
 * enforces, so nothing here has to handle a third.
 *
 * <p>
 * The server sends the categories flat, each naming its parent; this is the one place that turns that into a tree, so
 * the filter, the hero, the breadcrumb, the seller's form and the back office cannot each build it a little differently.
 * </p>
 */
export interface Department {
  category: Category
  children: Category[]
}

const byName = (a: Category, b: Category) => a.name.localeCompare(b.name)

/**
 * Departments by name, each with its categories by name. A category whose parent is not in the list - a stale list, or
 * a parent the caller filtered out - is treated as a department, so it is never hidden.
 */
export function categoryTree(categories: Category[]): Department[] {
  const ids = new Set(categories.map((c) => c.id))
  const isDepartment = (c: Category) => !c.parentCategoryId || !ids.has(c.parentCategoryId)

  return categories
    .filter(isDepartment)
    .sort(byName)
    .map((category) => ({
      category,
      children: categories.filter((c) => c.parentCategoryId === category.id).sort(byName),
    }))
}

/** The top level only - the landing page's chips and a "Department" picker. */
export function departments(categories: Category[]): Category[] {
  return categoryTree(categories).map((d) => d.category)
}

/** Every category in tree order: a department, then its categories, `depth` saying which. */
export function categoryRows(categories: Category[]): { category: Category; depth: 0 | 1; department?: Category }[] {
  return categoryTree(categories).flatMap((d) => [
    { category: d.category, depth: 0 as const },
    ...d.children.map((child) => ({ category: child, depth: 1 as const, department: d.category })),
  ])
}

/**
 * The choices of a category picker, in tree order. A category under a department is indented and carries the
 * department as its hint - shown under it, and searched too, so typing "điện tử" finds "Điện thoại".
 */
export function categoryChoices(categories: Category[]): Choice[] {
  return categoryRows(categories).map(({ category, depth, department }) => ({
    value: category.id,
    label: category.name,
    ...(depth === 1 && department ? { hint: department.name, indent: true } : {}),
  }))
}

/** Where a category sits: [department, category], or [category] for a department. Empty when it is not known. */
export function categoryPath(categories: Category[], id: string): Category[] {
  const category = categories.find((c) => c.id === id)
  if (!category) return []
  const parent = category.parentCategoryId ? categories.find((c) => c.id === category.parentCategoryId) : undefined
  return parent ? [parent, category] : [category]
}
