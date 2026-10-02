import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Shops } from '@ecommerce/core/services/shops'
import type { ShopState } from '@ecommerce/core/services/shops/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { ShopStateCard } from '.'

const open: ShopState = {
  sellerId: 's1', shopName: 'Mai Lens', state: 'Open', pausedAt: null, closedAt: null, closedReason: null,
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ShopStateCard (specs/107)', () => {
  /** Pausing takes every product off the shop, so it says so and asks first. */
  it('pauses an open shop after saying what that does', async () => {
    vi.spyOn(Shops, 'mine').mockResolvedValue(open)
    const pause = vi.spyOn(Shops, 'pause').mockResolvedValue({ ...open, state: 'Paused', pausedAt: '2026-09-27T08:00:00Z' })
    const user = userEvent.setup()
    renderAsSeller(<ShopStateCard />)

    await user.click(await screen.findByRole('button', { name: 'Pause shop' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/Orders already paid still wait for you/)).toBeInTheDocument()
    expect(pause).not.toHaveBeenCalled()

    await user.click(within(dialog).getByRole('button', { name: 'Pause shop' }))
    await waitFor(() => expect(pause).toHaveBeenCalledOnce())
  })

  it('reopens a paused shop at a press', async () => {
    vi.spyOn(Shops, 'mine').mockResolvedValue({ ...open, state: 'Paused', pausedAt: '2026-09-27T08:00:00Z' })
    const resume = vi.spyOn(Shops, 'resume').mockResolvedValue(open)
    const user = userEvent.setup()
    renderAsSeller(<ShopStateCard />)

    expect(await screen.findByText(/Paused since/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Pause shop' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reopen shop' }))

    await waitFor(() => expect(resume).toHaveBeenCalledOnce())
  })

  /** Only staff reopen a shop they closed: the seller reads why, and is offered nothing to press. */
  it('shows the reason for a closed shop and no button', async () => {
    vi.spyOn(Shops, 'mine').mockResolvedValue({
      ...open, state: 'Closed', closedAt: '2026-09-27T08:00:00Z', closedReason: 'Counterfeit listings', pausedAt: '2026-09-26T08:00:00Z',
    })
    renderAsSeller(<ShopStateCard />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Counterfeit listings')
    expect(screen.getByText('Closed by staff')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reopen shop' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Pause shop' })).not.toBeInTheDocument()
  })

  it('shows a refusal in the server’s words', async () => {
    vi.spyOn(Shops, 'mine').mockResolvedValue({ ...open, state: 'Paused', pausedAt: '2026-09-27T08:00:00Z' })
    vi.spyOn(Shops, 'resume').mockRejectedValue(refusal(409, 'Staff closed this shop: Checking'))
    const user = userEvent.setup()
    renderAsSeller(<ShopStateCard />)

    await user.click(await screen.findByRole('button', { name: 'Reopen shop' }))

    expect(await screen.findByText('Staff closed this shop: Checking')).toBeInTheDocument()
  })
})
