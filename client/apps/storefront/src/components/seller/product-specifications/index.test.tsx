import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Category } from '@ecommerce/core/services/category'
import type { Specification } from '@ecommerce/core/services/category/types'
import { Product } from '@ecommerce/core/services/product'
import type { Product as ProductModel } from '@ecommerce/core/services/product/types'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { ProductSpecificationsCard } from '.'

const brand: Specification = {
  id: 's-brand', categoryId: 'c-el', code: 'brand', name: 'Brand', kind: 'Choice', position: 0,
  options: [{ id: 'o-apple', code: 'apple', value: 'Apple' }, { id: 'o-samsung', code: 'samsung', value: 'Samsung' }],
}
const screenSize: Specification = { id: 's-screen', categoryId: 'c-ph', code: 'screen', name: 'Screen', kind: 'Text', position: 0, options: [] }
const storage: Specification = { id: 's-storage', categoryId: 'c-ph', code: 'storage', name: 'Storage', kind: 'Text', position: 1, options: [] }

const product = {
  id: 'p1', categoryId: 'c-ph',
  specifications: [{ specificationId: 's-brand', name: 'Brand', kind: 'Choice', optionId: 'o-apple', text: null, value: 'Apple' },
    { specificationId: 's-storage', name: 'Storage', kind: 'Text', optionId: null, text: '128GB', value: '128GB' }],
} as unknown as ProductModel

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'specifications').mockResolvedValue([brand, screenSize, storage])
})

describe('ProductSpecificationsCard (specs/159)', () => {
  it('fills in what is stored and sends the whole set: an option, a trimmed text, and nothing for an emptied field', async () => {
    const set = vi.spyOn(Product, 'setSpecifications').mockResolvedValue([])
    const user = userEvent.setup()
    renderAsSeller(<ProductSpecificationsCard product={product} />)

    expect(await screen.findByLabelText('Storage')).toHaveValue('128GB')
    await user.type(screen.getByLabelText('Screen'), '  6.1 inch ')
    await user.clear(screen.getByLabelText('Storage'))
    await user.click(screen.getByRole('button', { name: /Save specifications/ }))

    await waitFor(() =>
      expect(set).toHaveBeenCalledWith('p1', [
        { specificationId: 's-brand', optionId: 'o-apple' },
        { specificationId: 's-screen', text: '6.1 inch' },
      ]),
    )
  })

  it('has nothing to save until something changes', async () => {
    renderAsSeller(<ProductSpecificationsCard product={product} />)

    expect(await screen.findByRole('button', { name: /Save specifications/ })).toBeDisabled()
  })

  it('is not shown for a category with no specifications', async () => {
    vi.spyOn(Category, 'specifications').mockResolvedValue([])
    const { container } = renderAsSeller(<ProductSpecificationsCard product={product} />)

    await waitFor(() => expect(Category.specifications).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()
  })
})
