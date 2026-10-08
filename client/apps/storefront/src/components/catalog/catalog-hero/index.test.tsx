import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import type { Category } from '@ecommerce/core/services/category/types'
import { renderAsCustomer } from '@ecommerce/core/test/render'
import { CatalogHero } from '.'

const make = (id: string, name: string, parentCategoryId: string | null = null): Category => ({
  id, name, description: null, slug: id, parentCategoryId, isActive: true,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('CatalogHero (specs/158)', () => {
  it('offers the departments as chips, not every category, and a chip lists everything in it', async () => {
    const onCategory = vi.fn()
    renderAsCustomer(
      <CatalogHero
        productCount={41}
        categories={[make('ph', 'Phones', 'el'), make('el', 'Electronics'), make('bk', 'Books'), make('fi', 'Fiction', 'bk')]}
        onCategory={onCategory}
      />,
    )

    const chips = screen.getAllByRole('button').map((b) => b.textContent)
    expect(chips).toEqual(['Books', 'Electronics'])

    await userEvent.setup().click(screen.getByRole('button', { name: 'Electronics' }))
    expect(onCategory).toHaveBeenCalledWith('el')
  })
})
