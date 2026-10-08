import { describe, expect, it } from 'vitest'
import type { Category } from '@ecommerce/core/services/category/types'
import { categoryChoices, categoryPath, categoryRows, categoryTree, departments } from '.'

/** The flat list the server sends, each naming its parent (specs/158). */
const make = (id: string, name: string, parentCategoryId: string | null = null): Category => ({
  id, name, description: null, slug: id, parentCategoryId, isActive: true,
})
const electronics = make('el', 'Điện tử')
const phones = make('ph', 'Điện thoại', 'el')
const laptops = make('lt', 'Laptop', 'el')
const books = make('bk', 'Sách')
const fiction = make('fi', 'Văn học', 'bk')
// The server sends them in no particular order.
const all = [laptops, fiction, books, phones, electronics]

describe('categoryTree (specs/158)', () => {
  it('puts each department first and its categories under it, both by name', () => {
    expect(categoryTree(all).map((d) => [d.category.name, d.children.map((c) => c.name)])).toEqual([
      ['Điện tử', ['Điện thoại', 'Laptop']],
      ['Sách', ['Văn học']],
    ])
  })

  it('treats a category whose department is not in the list as a department, so it is never hidden', () => {
    expect(departments([phones, books]).map((c) => c.name)).toEqual(['Điện thoại', 'Sách'])
  })

  it('flattens to tree order with a depth and the department of each category', () => {
    expect(categoryRows(all).map((r) => [r.category.id, r.depth, r.department?.id])).toEqual([
      ['el', 0, undefined],
      ['ph', 1, 'el'],
      ['lt', 1, 'el'],
      ['bk', 0, undefined],
      ['fi', 1, 'bk'],
    ])
  })

  it('makes picker choices with categories indented and their department as the searchable hint', () => {
    const choices = categoryChoices(all)

    expect(choices[0]).toEqual({ value: 'el', label: 'Điện tử' })
    expect(choices[1]).toEqual({ value: 'ph', label: 'Điện thoại', hint: 'Điện tử', indent: true })
    expect(choices).toHaveLength(5)
  })

  it('says where a category sits: department then category, or the department alone', () => {
    expect(categoryPath(all, 'ph').map((c) => c.name)).toEqual(['Điện tử', 'Điện thoại'])
    expect(categoryPath(all, 'bk').map((c) => c.name)).toEqual(['Sách'])
    expect(categoryPath(all, 'gone')).toEqual([])
  })
})
