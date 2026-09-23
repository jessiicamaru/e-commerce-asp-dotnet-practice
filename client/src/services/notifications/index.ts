// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { NotificationPage } from './types'

/**
 * The caller's own inbox (specs/042). Nothing names a user: the server reads it from the token, and
 * someone else's notification is "not found".
 */
export class Notifications {
  static async list(page: number, pageSize: number, unreadOnly = false): Promise<NotificationPage> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (unreadOnly) params.set('unreadOnly', 'true')
    const { data } = await http.get<NotificationPage>(`/notifications?${params}`)
    return data
  }

  static async unreadCount(): Promise<number> {
    const { data } = await http.get<{ count: number }>('/notifications/unread-count')
    return data.count
  }

  static async markRead(id: string): Promise<void> {
    await http.post(`/notifications/${id}/read`)
  }

  static async markAllRead(): Promise<void> {
    await http.post('/notifications/read-all')
  }
}
