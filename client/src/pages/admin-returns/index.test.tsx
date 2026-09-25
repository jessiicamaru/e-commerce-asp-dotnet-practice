import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Admin } from '@/services/admin'
import type { ParcelReturn } from '@/services/order/types'
import { renderAsAdmin } from '@/test/render'
import { AdminReturnsPage } from '.'

const escalated: ParcelReturn = {
  id: 'r-1', orderId: 'o-7', shipmentId: 's-1', isShop: false, status: 'Escalated', reason: 'The shutter sticks',
  decisionReason: 'Worked when sent', trackingReference: null, requestedAt: '2026-09-24T08:00:00Z',
  decidedAt: '2026-09-24T10:00:00Z', sentBackAt: null, receivedAt: null, refundAmount: null,
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminReturnsPage (specs/067)', () => {
  it('opens on the disputes, each leading to its order', async () => {
    const list = vi.spyOn(Admin, 'returns').mockResolvedValue({ items: [escalated], page: 1, pageSize: 12, totalCount: 1 })
    renderAsAdmin(<AdminReturnsPage />, '/admin/returns')

    expect(await screen.findByText('The shutter sticks')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith('Escalated', 1, 12)
    expect(screen.getByText('Refused: Worked when sent')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /The shutter sticks/ })).toHaveAttribute('href', '/admin/orders/o-7')
  })

  it('asks for another state when its tab is chosen', async () => {
    const list = vi.spyOn(Admin, 'returns').mockResolvedValue({ items: [], page: 1, pageSize: 12, totalCount: 0 })
    const user = userEvent.setup()
    renderAsAdmin(<AdminReturnsPage />, '/admin/returns')

    expect(await screen.findByText('Nothing here.')).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Sent back' }))

    await waitFor(() => expect(list).toHaveBeenCalledWith('SentBack', 1, 12))
  })

  /** A state that is not a tab - typed into the address - falls back to the disputes rather than asking for it. */
  it('ignores a state in the address that is not one of its tabs', async () => {
    const list = vi.spyOn(Admin, 'returns').mockResolvedValue({ items: [], page: 1, pageSize: 12, totalCount: 0 })
    renderAsAdmin(<AdminReturnsPage />, '/admin/returns?status=Bogus')

    await screen.findByText('Nothing here.')
    expect(list).toHaveBeenCalledWith('Escalated', 1, 12)
  })
})
