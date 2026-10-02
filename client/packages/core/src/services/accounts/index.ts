// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@ecommerce/core/config/axios'
import type { Account, AccountPage, ModerationHistoryPage, AccountFilter } from './types'

/**
 * People, as staff look after them (specs/043) - Identity through the gateway. Looking people up and
 * locking are for Admin or Moderator; roles and bans are for Admin. The server enforces every one of
 * those rules, and the ones about WHO may be locked, on its own.
 */
export class Accounts {
  /** Deleted accounts are left out unless `includeDeleted` (specs/123). */
  static async search(
    search: string, page: number, pageSize: number, includeDeleted = false, filter: AccountFilter = {},
  ): Promise<AccountPage> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) params.set('search', search)
    if (includeDeleted) params.set('includeDeleted', 'true')
    // By role and by state (specs/133); left out when not chosen.
    if (filter.role) params.set('role', filter.role)
    if (filter.state) params.set('state', filter.state)
    const { data } = await http.get<AccountPage>(`/users?${params}`)
    return data
  }

  /** What staff decided about this person before, newest first (specs/100) - Activity, Moderation entries only. */
  static async history(id: string, page: number, pageSize: number): Promise<ModerationHistoryPage> {
    const { data } = await http.get<ModerationHistoryPage>(`/audit/people/${id}?page=${page}&pageSize=${pageSize}`)
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

  /** Resets another person's two-factor sign-in (specs/110): Admin, never oneself; their sessions end, they are emailed. */
  static async resetTwoFactor(id: string): Promise<void> {
    await http.delete(`/users/${id}/two-factor`)
  }
}
