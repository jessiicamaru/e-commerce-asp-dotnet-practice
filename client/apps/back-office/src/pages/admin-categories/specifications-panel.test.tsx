import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Category } from '@ecommerce/core/services/category'
import type { Category as CategoryModel, Specification } from '@ecommerce/core/services/category/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsAdmin } from '@ecommerce/core/test/render'
import { parseOptionLines, SpecificationsPanel } from './specifications-panel'

const fashion: CategoryModel = {
  id: 'c-fa', name: 'Thời trang', description: null, slug: 'thoi-trang', parentCategoryId: null, isActive: true, language: 'vi',
}
const material: Specification = {
  id: 's-mat', categoryId: 'c-fa', code: 'chat-lieu', name: 'Chất liệu', kind: 'Choice', position: 0, language: 'vi',
  options: [{ id: 'o-bong', code: 'bong', value: 'Bông', language: 'vi' }],
}
// The department's, listed for its categories, not editable here.
const inherited: Specification = { ...material, id: 's-other', categoryId: 'c-other', name: 'Khác' }

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'specificationsIn').mockImplementation(async (_, language) =>
    language === 'en'
      ? [{ ...material, name: 'Material', language: 'en', options: [{ ...material.options[0], value: 'Cotton', language: 'en' }] }]
      : [material, inherited],
  )
})

describe('parseOptionLines (specs/159)', () => {
  it('reads "Tiếng Việt | English" per line, the English optional, the code from the Vietnamese', () => {
    expect(parseOptionLines('Bông | Cotton\n\n  Da  \n')).toEqual([
      { code: 'bong', value: 'Bông', english: 'Cotton' },
      { code: 'da', value: 'Da', english: '' },
    ])
  })
})

describe('SpecificationsPanel (specs/159)', () => {
  it("lists the category's own specifications with their English and options", async () => {
    renderAsAdmin(<SpecificationsPanel category={fashion} />, '/categories')

    expect(await screen.findByText('Chất liệu')).toBeInTheDocument()
    expect(screen.getByText(/· Material/)).toBeInTheDocument()
    expect(screen.getByText(/Cotton/)).toBeInTheDocument()
    expect(screen.queryByText('Khác')).toBeNull()
  })

  it('creates a choice with its options and their English', async () => {
    const created: Specification = {
      ...material, id: 's-new', code: 'kieu-dang', name: 'Kiểu dáng',
      options: [{ id: 'o1', code: 'om', value: 'Ôm' }, { id: 'o2', code: 'rong', value: 'Rộng' }],
    }
    const create = vi.spyOn(Category, 'createSpecification').mockResolvedValue(created)
    const translate = vi.spyOn(Category, 'translateSpecification').mockResolvedValue(created)
    const translateOption = vi.spyOn(Category, 'translateOption').mockResolvedValue(created)
    const user = userEvent.setup()
    renderAsAdmin(<SpecificationsPanel category={fashion} />, '/categories')

    await user.type(await screen.findByLabelText('Specification (Vietnamese)'), 'Kiểu dáng')
    await user.type(screen.getByLabelText('English'), 'Fit')
    await user.type(screen.getByLabelText(/Options, one per line/), 'Ôm | Slim{enter}Rộng')
    await user.click(screen.getByRole('button', { name: /Add/ }))

    await waitFor(() =>
      expect(create).toHaveBeenCalledWith('c-fa', {
        code: 'kieu-dang', name: 'Kiểu dáng', kind: 'Choice',
        options: [{ code: 'om', value: 'Ôm' }, { code: 'rong', value: 'Rộng' }],
      }),
    )
    await waitFor(() => expect(translate).toHaveBeenCalledWith('c-fa', 's-new', 'en', 'Fit'))
    // Only the option given an English is translated.
    expect(translateOption).toHaveBeenCalledTimes(1)
    expect(translateOption).toHaveBeenCalledWith('c-fa', 's-new', 'o1', 'en', 'Slim')
  })

  it('shows the refusal to remove what a product uses in its words', async () => {
    vi.spyOn(Category, 'removeSpecification').mockRejectedValue(refusal(409, '3 product(s) have a value for "Chất liệu". Clear them first.'))
    const user = userEvent.setup()
    renderAsAdmin(<SpecificationsPanel category={fashion} />, '/categories')

    await screen.findByText('Chất liệu')
    await user.click(screen.getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText('3 product(s) have a value for "Chất liệu". Clear them first.')).toBeInTheDocument()
  })
})
