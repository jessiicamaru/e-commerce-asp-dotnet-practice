import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Notifications } from '@/services/notifications'
import type { AppNotification } from '@/services/notifications/types'
import { renderAsSeller } from '@/test/render'
import { BELL_SIZE, NotificationBell } from '.'

const sale: AppNotification = {
  id: 'n-1',
  kind: 'NewSale',
  data: { orderId: '01a0cee7-aaaa' },
  link: '/shop/sales/01a0cee7-aaaa',
  createdAt: '2026-09-24T08:00:00Z',
  readAt: null,
}

function renderBell() {
  return renderAsSeller(
    <Routes>
      <Route path="/" element={<NotificationBell />} />
      <Route path="/shop/sales/:id" element={<p>the sale</p>} />
    </Routes>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('NotificationBell', () => {
  it('shows how many are unread', async () => {
    vi.spyOn(Notifications, 'unreadCount').mockResolvedValue(3)
    renderBell()

    expect(await screen.findByRole('button', { name: 'Notifications, 3 unread' })).toHaveTextContent('3')
  })

  /** Loaded when opened, not on every page: the poll is kept to one count. */
  it('loads the latest only when opened, and a choice marks it read and goes there', async () => {
    vi.spyOn(Notifications, 'unreadCount').mockResolvedValue(1)
    const list = vi.spyOn(Notifications, 'list').mockResolvedValue({ items: [sale], page: 1, pageSize: BELL_SIZE, totalCount: 1 })
    const markRead = vi.spyOn(Notifications, 'markRead').mockResolvedValue()
    const user = userEvent.setup()
    renderBell()
    await screen.findByRole('button', { name: /1 unread/ })
    expect(list).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: /1 unread/ }))
    await user.click(await screen.findByRole('menuitem', { name: /You have a new sale: 01a0cee7/ }))

    await waitFor(() => expect(markRead).toHaveBeenCalledWith('n-1'))
    expect(await screen.findByText('the sale')).toBeInTheDocument()
  })

  it('marks everything read at once', async () => {
    vi.spyOn(Notifications, 'unreadCount').mockResolvedValue(2)
    vi.spyOn(Notifications, 'list').mockResolvedValue({ items: [sale], page: 1, pageSize: BELL_SIZE, totalCount: 1 })
    const all = vi.spyOn(Notifications, 'markAllRead').mockResolvedValue()
    const user = userEvent.setup()
    renderBell()

    await user.click(await screen.findByRole('button', { name: /2 unread/ }))
    await user.click(await screen.findByRole('menuitem', { name: /Mark all read/ }))

    await waitFor(() => expect(all).toHaveBeenCalled())
  })
})
