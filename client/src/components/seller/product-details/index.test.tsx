import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Category } from '@/services/category'
import { Product } from '@/services/product'
import type { Product as ProductModel } from '@/services/product/types'
import { refusal } from '@/test/refusal'
import { renderAsSeller } from '@/test/render'
import { ProductDetailsCard } from '.'

/** Read in English: the name above falls back per field, the original is what was typed when listing. */
const listing = {
  id: 'p1', name: 'Canon EOS R50 (English)', description: 'Small.', categoryId: 'c1',
  original: { name: 'Máy ảnh Canon EOS R50', description: 'Nhỏ gọn.' },
} as unknown as ProductModel

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Category, 'list').mockResolvedValue([
    { id: 'c1', name: 'Mirrorless cameras', description: null, slug: 'mirrorless', parentCategoryId: null, isActive: true },
    { id: 'c2', name: 'Compact cameras', description: null, slug: 'compact', parentCategoryId: null, isActive: true },
  ])
})

describe('ProductDetailsCard (specs/124, #240)', () => {
  it('edits the original text, not the language being read in', () => {
    renderAsSeller(<ProductDetailsCard product={listing} />)

    expect(screen.getByLabelText('Name')).toHaveValue('Máy ảnh Canon EOS R50')
    expect(screen.getByLabelText('Description')).toHaveValue('Nhỏ gọn.')
  })

  it('offers to save only once something changed, and sends it trimmed with an empty description as none', async () => {
    const update = vi.spyOn(Product, 'updateDetails').mockResolvedValue(listing)
    const user = userEvent.setup()
    renderAsSeller(<ProductDetailsCard product={listing} />)
    const save = screen.getByRole('button', { name: 'Save details' })
    expect(save).toBeDisabled()

    await user.clear(screen.getByLabelText('Name'))
    await user.type(screen.getByLabelText('Name'), '  Canon EOS R50 V  ')
    await user.clear(screen.getByLabelText('Description'))
    await user.click(save)

    await waitFor(() =>
      expect(update).toHaveBeenCalledWith('p1', { name: 'Canon EOS R50 V', description: null, categoryId: 'c1' }),
    )
  })

  it('shows the server refusal in its words', async () => {
    vi.spyOn(Product, 'updateDetails').mockRejectedValue(refusal(404, "Product with ID 'p1' was not found."))
    const user = userEvent.setup()
    renderAsSeller(<ProductDetailsCard product={listing} />)

    await user.type(screen.getByLabelText('Name'), ' II')
    await user.click(screen.getByRole('button', { name: 'Save details' }))

    expect(await screen.findByText(/was not found/)).toBeInTheDocument()
    expect(screen.queryByText(/not allowed|forbidden|permission/i)).not.toBeInTheDocument()
  })
})
