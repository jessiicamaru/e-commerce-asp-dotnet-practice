import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { Notifications } from '.'

describe('Notifications', () => {
  /** The caller's own inbox: nothing in any request names a user (specs/042). */
  it('reads and marks the caller own inbox, naming nobody', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: { count: 3, items: [] } })
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await Notifications.list(2, 12, true)
    expect(await Notifications.unreadCount()).toBe(3)
    await Notifications.markRead('n-1')
    await Notifications.markAllRead()

    expect(get.mock.calls.map((c) => c[0])).toEqual([
      '/notifications?page=2&pageSize=12&unreadOnly=true',
      '/notifications/unread-count',
    ])
    expect(post.mock.calls.map((c) => c[0])).toEqual(['/notifications/n-1/read', '/notifications/read-all'])
  })
})
