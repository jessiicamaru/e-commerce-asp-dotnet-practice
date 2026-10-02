import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Product } from '@ecommerce/core/services/product'
import type { Product as ProductModel } from '@ecommerce/core/services/product/types'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { ProductTranslationsCard } from '.'

/** English has its own name and NO description of its own: the stored null, not the original's description. */
const listing = {
  id: 'p1', name: 'Canon EOS R50', description: 'Nhỏ gọn.',
  original: { name: 'Canon EOS R50', description: 'Nhỏ gọn.' },
  translations: [{ language: 'en', name: 'Canon EOS R50 mirrorless', description: null }],
} as unknown as ProductModel

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ProductTranslationsCard (specs/124, #240)', () => {
  it('offers every language, saying which have their own text, as stored', () => {
    renderAsSeller(<ProductTranslationsCard product={listing} />)

    const english = screen.getByRole('form', { name: 'English' })
    expect(within(english).getByText('Own text')).toBeInTheDocument()
    expect(within(english).getByLabelText('Name')).toHaveValue('Canon EOS R50 mirrorless')
    expect(within(english).getByLabelText('Description')).toHaveValue('')

    const vietnamese = screen.getByRole('form', { name: 'Tiếng Việt' })
    expect(within(vietnamese).getByText('None yet - Tiếng Việt readers see the original.')).toBeInTheDocument()
    expect(within(vietnamese).queryByRole('button', { name: /Remove/ })).not.toBeInTheDocument()
  })

  it("saves a language's own text", async () => {
    const set = vi.spyOn(Product, 'setTranslation').mockResolvedValue()
    vi.spyOn(Product, 'getAsOwner').mockResolvedValue(listing)
    const user = userEvent.setup()
    renderAsSeller(<ProductTranslationsCard product={listing} />)
    const vietnamese = screen.getByRole('form', { name: 'Tiếng Việt' })

    await user.type(within(vietnamese).getByLabelText('Name'), ' Máy ảnh Canon EOS R50 ')
    await user.click(within(vietnamese).getByRole('button', { name: 'Save Tiếng Việt' }))

    await waitFor(() => expect(set).toHaveBeenCalledWith('p1', 'vi', { name: 'Máy ảnh Canon EOS R50', description: null }))
  })

  it("takes a language's own text away", async () => {
    const remove = vi.spyOn(Product, 'removeTranslation').mockResolvedValue()
    const user = userEvent.setup()
    renderAsSeller(<ProductTranslationsCard product={listing} />)

    await user.click(within(screen.getByRole('form', { name: 'English' })).getByRole('button', { name: 'Remove English' }))

    await waitFor(() => expect(remove).toHaveBeenCalledWith('p1', 'en'))
  })
})
