import { describe, expect, it, vi } from 'vitest'
import { http } from '@/config/axios'
import { NotificationWording } from '.'

describe('NotificationWording (specs/078)', () => {
  it('reads, saves on top of the version it opened, resets and restores', async () => {
    const get = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })
    const put = vi.spyOn(http, 'put').mockResolvedValue({ data: {} })
    const post = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })

    await NotificationWording.current()
    await NotificationWording.overview()
    await NotificationWording.versions('NewReview_one', 'en')
    await NotificationWording.save('NewSale', 'vi', 'Đơn {{order}}', 2)
    await NotificationWording.reset('NewSale', 'vi', 3)
    await NotificationWording.restore('NewSale', 'vi', 1, 4)

    expect(get.mock.calls.map((c) => c[0])).toEqual([
      '/notifications/wording',
      '/notifications/wording/all',
      '/notifications/wording/NewReview_one/en/versions',
    ])
    expect(put.mock.calls).toEqual([['/notifications/wording/NewSale/vi', { text: 'Đơn {{order}}', expectedVersion: 2 }]])
    expect(post.mock.calls).toEqual([
      ['/notifications/wording/NewSale/vi/reset', { expectedVersion: 3 }],
      ['/notifications/wording/NewSale/vi/versions/1/restore', { expectedVersion: 4 }],
    ])
  })
})
