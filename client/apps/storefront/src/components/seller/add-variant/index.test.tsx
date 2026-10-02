import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Product } from '@ecommerce/core/services/product'
import type { Product as ProductModel } from '@ecommerce/core/services/product/types'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { AddVariantForm } from '.'

const listing = {
  id: 'p1',
  variants: [
    { id: 'p1', options: [{ id: 'o1', name: 'Kit', value: 'Body only' }, { id: 'o2', name: 'Colour', value: 'Black' }] },
  ],
} as unknown as ProductModel

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AddVariantForm (specs/124, #240)', () => {
  it('starts from the option names the product already uses', () => {
    renderAsSeller(<AddVariantForm product={listing} />)

    expect(screen.getByLabelText('Option 1 name')).toHaveValue('Kit')
    expect(screen.getByLabelText('Option 2 name')).toHaveValue('Colour')
    expect(screen.getByLabelText('Option 1 value')).toHaveValue('')
  })

  it('starts a product sold one way with an empty option to tell the new shape apart', () => {
    renderAsSeller(<AddVariantForm product={{ id: 'p2', variants: [{ id: 'p2', options: [] }] } as unknown as ProductModel} />)

    expect(screen.getByLabelText('Option 1 name')).toHaveValue('')
    expect(screen.getByText(/Sold one way so far/)).toBeInTheDocument()
  })

  it('adds the variant with its price as a number in the default currency, leaving out blank options', async () => {
    const add = vi.spyOn(Product, 'addVariant').mockResolvedValue()
    const user = userEvent.setup()
    renderAsSeller(<AddVariantForm product={listing} />)
    const submit = screen.getByRole('button', { name: 'Add variant' })
    expect(submit).toBeDisabled()

    await user.type(screen.getByLabelText('SKU'), ' CANON-R50-SILVER ')
    await user.type(screen.getByLabelText("The new variant's price in VND"), '18900000')
    await user.type(screen.getByLabelText('Option 1 value'), 'Body only')
    await user.click(submit)

    await waitFor(() =>
      expect(add).toHaveBeenCalledWith('p1', {
        sku: 'CANON-R50-SILVER',
        price: 18_900_000,
        options: [{ name: 'Kit', value: 'Body only' }],
      }),
    )
  })
})
