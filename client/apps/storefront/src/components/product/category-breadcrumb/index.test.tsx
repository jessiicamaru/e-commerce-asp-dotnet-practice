import { screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Category } from '@ecommerce/core/services/category'
import type { Category as CategoryModel } from '@ecommerce/core/services/category/types'
import { renderAsCustomer } from '@ecommerce/core/test/render'
import { CategoryBreadcrumb } from '.'

const electronics: CategoryModel = {
  id: 'c-el', name: 'Electronics', description: null, slug: 'dien-tu', parentCategoryId: null, isActive: true,
}
const phones: CategoryModel = { ...electronics, id: 'c-ph', name: 'Phones', slug: 'dien-thoai', parentCategoryId: 'c-el' }

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'list').mockResolvedValue([phones, electronics])
})

describe('CategoryBreadcrumb (specs/158)', () => {
  it('says where a product sits, each step a link to the listing filtered by it', async () => {
    renderAsCustomer(<CategoryBreadcrumb categoryId="c-ph" />)

    const nav = await screen.findByRole('navigation', { name: 'Where it sits' })
    const links = Array.from(nav.querySelectorAll('a'))
    expect(links.map((a) => [a.textContent, a.getAttribute('href')])).toEqual([
      ['Electronics', '/?category=c-el'],
      ['Phones', '/?category=c-ph'],
    ])
  })

  it('draws nothing for a category it does not know', async () => {
    const { container } = renderAsCustomer(<CategoryBreadcrumb categoryId="c-gone" />)

    await vi.waitFor(() => expect(Category.list).toHaveBeenCalled())
    expect(container.querySelector('nav')).toBeNull()
  })
})
