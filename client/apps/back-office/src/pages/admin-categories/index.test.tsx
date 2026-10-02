import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Category } from '@ecommerce/core/services/category'
import type { Category as CategoryModel } from '@ecommerce/core/services/category/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsAdmin } from '@ecommerce/core/test/render'
import { slugOf } from '@ecommerce/core/utils/shared'
import { AdminCategoriesPage } from '.'

const lenses: CategoryModel = {
  id: 'c-lens', name: 'Ống kính', description: null, slug: 'ong-kinh', parentCategoryId: null, isActive: false, language: 'vi',
}
const cameras: CategoryModel = {
  id: 'c-cam', name: 'Máy ảnh', description: 'Thân máy', slug: 'may-anh', parentCategoryId: null, isActive: false, language: 'vi',
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'listIn').mockImplementation(async (language) =>
    language === 'en'
      ? [{ ...lenses, name: 'Lenses', language: 'en' }, { ...cameras, language: 'vi' }]
      : [lenses, cameras],
  )
})

describe('AdminCategoriesPage (specs/097)', () => {
  it('lists each category in Vietnamese with its English, and says when there is none', async () => {
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    expect(await screen.findByText('Ống kính')).toBeInTheDocument()
    expect(screen.getByText(/Lenses/)).toBeInTheDocument()
    // Máy ảnh's English list answered in Vietnamese: not translated.
    const row = screen.getByText('Máy ảnh').closest('li')!
    expect(within(row).getByText(/No English yet/)).toBeInTheDocument()
  })

  it('creates a category at the address its name suggests', async () => {
    const create = vi.spyOn(Category, 'create').mockResolvedValue({ ...lenses, id: 'c-new', name: 'Đèn flash', slug: 'den-flash' })
    const user = userEvent.setup()
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    await user.type(await screen.findByLabelText('Name'), 'Đèn flash')
    expect(screen.getByLabelText('Address')).toHaveValue('den-flash')
    await user.click(screen.getByRole('button', { name: /Create/ }))

    await waitFor(() => expect(create).toHaveBeenCalledWith('Đèn flash', 'den-flash', null))
  })

  it('shows the refusal of an address already taken in its words', async () => {
    vi.spyOn(Category, 'create').mockRejectedValue(refusal(409, "A category with the address 'ong-kinh' already exists."))
    const user = userEvent.setup()
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    await user.type(await screen.findByLabelText('Name'), 'Ống kính')
    await user.click(screen.getByRole('button', { name: /Create/ }))

    expect(await screen.findByText("A category with the address 'ong-kinh' already exists.")).toBeInTheDocument()
  })

  it('renames in Vietnamese and translates into English, each to its own endpoint', async () => {
    const update = vi.spyOn(Category, 'update').mockResolvedValue(cameras)
    const translate = vi.spyOn(Category, 'translate').mockResolvedValue(cameras)
    const user = userEvent.setup()
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    const row = (await screen.findByText('Máy ảnh')).closest('li')!
    await user.click(within(row).getByRole('button', { name: 'Edit' }))
    const name = within(row).getByLabelText('Name', { selector: '#c-cam-vi-name' })
    await user.clear(name)
    await user.type(name, 'Máy ảnh số')
    await user.type(within(row).getByLabelText('Name', { selector: '#c-cam-en-name' }), 'Cameras')
    await user.click(within(row).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(update).toHaveBeenCalledWith('c-cam', 'Máy ảnh số', 'Thân máy'))
    expect(translate).toHaveBeenCalledWith('c-cam', 'en', 'Cameras', null)
  })

  it('removes the English when its name is emptied', async () => {
    vi.spyOn(Category, 'update').mockResolvedValue(lenses)
    const remove = vi.spyOn(Category, 'removeTranslation').mockResolvedValue()
    const user = userEvent.setup()
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    const row = (await screen.findByText('Ống kính')).closest('li')!
    await user.click(within(row).getByRole('button', { name: 'Edit' }))
    await user.clear(within(row).getByLabelText('Name', { selector: '#c-lens-en-name' }))
    await user.click(within(row).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(remove).toHaveBeenCalledWith('c-lens', 'en'))
  })

  it('asks before deleting, and shows why a category with products stays', async () => {
    const del = vi.spyOn(Category, 'remove').mockRejectedValue(refusal(409, 'This category still has products.'))
    const user = userEvent.setup()
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    const row = (await screen.findByText('Ống kính')).closest('li')!
    await user.click(within(row).getByRole('button', { name: 'Delete' }))
    expect(del).not.toHaveBeenCalled()
    await user.click(within(row).getByRole('button', { name: 'Delete for good' }))

    expect(await within(row).findByText('This category still has products.')).toBeInTheDocument()
    expect(del).toHaveBeenCalledWith('c-lens')
  })
})

describe('slugOf', () => {
  it('takes the accents and đ off and joins the words with hyphens', () => {
    expect(slugOf('  Ống kính  rời / Đà Nẵng ')).toBe('ong-kinh-roi-da-nang')
  })
})

describe('categories searched and paged (specs/133, #249)', () => {
  it('narrows by a name in either language or the address', async () => {
    const user = userEvent.setup()
    renderAsAdmin(<AdminCategoriesPage />, '/categories')
    await screen.findByText('Máy ảnh')

    await user.type(screen.getByRole('textbox', { name: 'Name or address' }), 'lenses')

    expect(screen.queryByText('Máy ảnh')).not.toBeInTheDocument()
    expect(screen.getByText('Ống kính')).toBeInTheDocument()
  })

  it('shows twelve to a page', async () => {
    vi.mocked(Category.listIn).mockImplementation(async () =>
      Array.from({ length: 14 }, (_, i) => ({ ...lenses, id: `c-${i}`, name: `Danh mục ${String(i).padStart(2, '0')}`, slug: `dm-${i}` })),
    )
    renderAsAdmin(<AdminCategoriesPage />, '/categories')

    expect(await screen.findByText('Danh mục 11')).toBeInTheDocument()
    expect(screen.queryByText('Danh mục 12')).not.toBeInTheDocument()
  })
})
