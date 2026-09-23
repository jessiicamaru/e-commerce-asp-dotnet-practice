import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Notifications } from '@/services/notifications'
import { renderAsSeller } from '@/test/render'
import { NotificationsPage } from '.'

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Notifications, 'unreadCount').mockResolvedValue(0)
})

describe('NotificationsPage', () => {
  it('lists everything, and only the unread on that tab', async () => {
    const list = vi.spyOn(Notifications, 'list').mockResolvedValue({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 })
    renderAsSeller(
      <Routes>
        <Route path="/notifications" element={<NotificationsPage />} />
      </Routes>,
      '/notifications',
    )
    await screen.findByText('No notifications yet.')
    expect(list).toHaveBeenCalledWith(1, PAGE_SIZE, false)

    await userEvent.click(screen.getByRole('tab', { name: /Unread/ }))

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(1, PAGE_SIZE, true))
  })
})
