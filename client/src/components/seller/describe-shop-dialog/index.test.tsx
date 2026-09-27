import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Seller } from '@/services/seller'
import { renderAsSeller } from '@/test/render'
import { DescribeShopDialog } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('DescribeShopDialog', () => {
  /** What is sent (specs/099): the words trimmed, and emptying the box clears the description rather than saving spaces. */
  it('sends the trimmed words, and null for an empty box', async () => {
    const describeShop = vi.spyOn(Seller, 'describe').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: null })
    const user = userEvent.setup()
    renderAsSeller(<DescribeShopDialog current="Old words" />)

    await user.click(screen.getByRole('button', { name: /Description/ }))
    const box = await screen.findByRole('textbox')
    await user.clear(box)
    await user.type(box, '   ')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(describeShop).toHaveBeenCalledWith(null))

    await user.click(screen.getByRole('button', { name: /Description/ }))
    const again = await screen.findByRole('textbox')
    await user.clear(again)
    await user.type(again, '  Used Fujifilm bodies.  ')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(describeShop).toHaveBeenLastCalledWith('Used Fujifilm bodies.'))
  })
})
