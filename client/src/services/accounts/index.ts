// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { Account, AccountPage } from './types'

/**
 * People, as staff look after them (specs/043) - Identity through the gateway. Looking people up and
 * locking are for Admin or Moderator; roles and bans are for Admin. The server enforces every one of
 * those rules, and the ones about WHO may be locked, on its own.
 */
export class Accounts {
  static async search(search: string, page: number, pageSize: number): Promise<AccountPage> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) params.set('search', search)
    const { data } = await http.get<AccountPage>(`/users?${params}`)
    return data
  }

  static async grantModerator(id: string): Promise<Account> {
    const { data } = await http.put<Account>(`/users/${id}/roles/Moderator`)
    return data
  }

  static async revokeModerator(id: string): Promise<Account> {
    const { data } = await http.delete<Account>(`/users/${id}/roles/Moderator`)
    return data
  }

  static async lock(id: string, days: number, reason: string): Promise<Account> {
    const { data } = await http.post<Account>(`/users/${id}/lock`, { days, reason })
    return data
  }

  static async unlock(id: string): Promise<Account> {
    const { data } = await http.post<Account>(`/users/${id}/unlock`)
    return data
  }

  static async ban(id: string, reason: string): Promise<Account> {
    const { data } = await http.post<Account>(`/users/${id}/ban`, { reason })
    return data
  }

  static async liftBan(id: string): Promise<Account> {
    const { data } = await http.post<Account>(`/users/${id}/unban`)
    return data
  }
}
