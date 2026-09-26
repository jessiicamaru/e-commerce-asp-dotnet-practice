import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { SavedProduct } from '@/services/saved-product'
import { renderAsCustomer, renderSignedOut } from '@/test/render'
import { SaveButton } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('SaveButton (specs/075)', () => {
  it('saves a product that is not saved, and says so once it is', async () => {
    vi.spyOn(SavedProduct, 'ids').mockResolvedValueOnce([]).mockResolvedValue(['p1'])
    const save = vi.spyOn(SavedProduct, 'save').mockResolvedValue()
    const user = userEvent.setup()
    renderAsCustomer(<SaveButton productId="p1" />)

    const heart = await screen.findByRole('button', { name: 'Save for later' })
    expect(heart).toHaveAttribute('aria-pressed', 'false')
    await user.click(heart)

    await waitFor(() => expect(save).toHaveBeenCalledWith('p1'))
    expect(await screen.findByRole('button', { name: 'Remove from saved' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('unsaves a product that is saved', async () => {
    vi.spyOn(SavedProduct, 'ids').mockResolvedValue(['p1'])
    const unsave = vi.spyOn(SavedProduct, 'unsave').mockResolvedValue()
    const user = userEvent.setup()
    renderAsCustomer(<SaveButton productId="p1" />)

    await user.click(await screen.findByRole('button', { name: 'Remove from saved' }))

    await waitFor(() => expect(unsave).toHaveBeenCalledWith('p1'))
  })

  /** Saving is the shopper's own: signed out, the heart asks nobody anything and sends them to sign in. */
  it('sends a signed-out visitor to sign in and asks the server nothing', async () => {
    const ids = vi.spyOn(SavedProduct, 'ids')
    const save = vi.spyOn(SavedProduct, 'save')
    const user = userEvent.setup()
    renderSignedOut(
      <Routes>
        <Route path="/products/p1" element={<SaveButton productId="p1" />} />
        <Route path="/sign-in" element={<p>the sign-in page</p>} />
      </Routes>,
      '/products/p1',
    )

    await user.click(screen.getByRole('button', { name: 'Save for later' }))

    expect(await screen.findByText('the sign-in page')).toBeInTheDocument()
    expect(ids).not.toHaveBeenCalled()
    expect(save).not.toHaveBeenCalled()
  })
})
