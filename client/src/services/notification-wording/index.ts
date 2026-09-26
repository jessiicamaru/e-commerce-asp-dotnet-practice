// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { WordingEntry, WordingOverrides, WordingOverview } from './types'

/**
 * The words of the notifications (specs/078, #150). The current edits are public - the storefront lays them over
 * its own words for everybody; changing them is an administrator's. The server sanitises and checks placeholders.
 */
export class NotificationWording {
  static async current(): Promise<WordingOverrides> {
    const { data } = await http.get<WordingOverrides>('/notifications/wording')
    return data
  }

  static async overview(): Promise<WordingOverview> {
    const { data } = await http.get<WordingOverview>('/notifications/wording/all')
    return data
  }

  static async versions(key: string, language: string): Promise<WordingEntry[]> {
    const { data } = await http.get<WordingEntry[]>(`/notifications/wording/${key}/${language}/versions`)
    return data
  }

  /** `expectedVersion` is the version the editor opened: somebody else's save since then is a 409. */
  static async save(key: string, language: string, text: string, expectedVersion: number): Promise<WordingEntry> {
    const { data } = await http.put<WordingEntry>(`/notifications/wording/${key}/${language}`, { text, expectedVersion })
    return data
  }

  static async reset(key: string, language: string, expectedVersion: number): Promise<WordingEntry> {
    const { data } = await http.post<WordingEntry>(`/notifications/wording/${key}/${language}/reset`, { expectedVersion })
    return data
  }

  static async restore(key: string, language: string, version: number, expectedVersion: number): Promise<WordingEntry> {
    const { data } = await http.post<WordingEntry>(`/notifications/wording/${key}/${language}/versions/${version}/restore`, {
      expectedVersion,
    })
    return data
  }
}
