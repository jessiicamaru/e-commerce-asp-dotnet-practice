import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Category } from '@ecommerce/core/services/category'
import type { Specification } from '@ecommerce/core/services/category/types'
import { renderAsCustomer } from '@ecommerce/core/test/render'
import { SpecificationFilters } from '.'

const brand: Specification = {
  id: 's-brand', categoryId: 'c-el', code: 'brand', name: 'Brand', kind: 'Choice', position: 0,
  options: [{ id: 'o-apple', code: 'apple', value: 'Apple' }, { id: 'o-samsung', code: 'samsung', value: 'Samsung' }],
}
const screenSize: Specification = { id: 's-screen', categoryId: 'c-ph', code: 'screen', name: 'Screen', kind: 'Text', position: 0, options: [] }

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'specifications').mockResolvedValue([brand, screenSize])
})

describe('SpecificationFilters (specs/159)', () => {
  it('offers the choice specifications of the chosen category, not the text ones', async () => {
    renderAsCustomer(<SpecificationFilters categoryId="c-ph" optionIds={[]} onChange={() => {}} />)

    expect(await screen.findByRole('combobox', { name: 'Brand' })).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Screen' })).toBeNull()
    expect(Category.specifications).toHaveBeenCalledWith('c-ph')
  })

  it('replaces the option of the same specification and keeps the others', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    renderAsCustomer(<SpecificationFilters categoryId="c-ph" optionIds={['o-apple', 'o-other']} onChange={onChange} />)

    await user.click(await screen.findByRole('combobox', { name: 'Brand' }))
    await user.click(await screen.findByRole('option', { name: 'Samsung' }))

    expect(onChange).toHaveBeenCalledWith(['o-other', 'o-samsung'])
  })

  it('asks nothing and draws nothing until a category is chosen', () => {
    const { container } = renderAsCustomer(<SpecificationFilters categoryId="" optionIds={[]} onChange={() => {}} />)

    expect(Category.specifications).not.toHaveBeenCalled()
    expect(container).toBeEmptyDOMElement()
  })
})
