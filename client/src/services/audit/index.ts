// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { AuditEntry, AuditFilter, AuditPage, CategoryCount } from './types'

/**
 * The audit log (specs/041), from the Activity service through the gateway. Administrators only - the
 * server refuses everyone else; the console drawing itself only for them is courtesy.
 */
export class Audit {
  static async list(filter: AuditFilter, page: number, pageSize: number): Promise<AuditPage> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    for (const [key, value] of Object.entries(filter)) {
      if (value) params.set(key, value)
    }

    const { data } = await http.get<AuditPage>(`/audit?${params}`)
    return data
  }

  static async get(id: string): Promise<AuditEntry> {
    const { data } = await http.get<AuditEntry>(`/audit/${id}`)
    return data
  }

  static async summary(from?: string): Promise<CategoryCount[]> {
    const { data } = await http.get<CategoryCount[]>(from ? `/audit/summary?from=${encodeURIComponent(from)}` : '/audit/summary')
    return data
  }
}
