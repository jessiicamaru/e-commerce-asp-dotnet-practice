// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { OutgoingEmail, OutgoingEmailPage, OutgoingEmailStatus } from './types'

/** The emails the shop sent, and the ones it could not (specs/087, #175) - administrators only. */
export class OutgoingEmails {
  static async list(status: OutgoingEmailStatus, search: string, page: number, pageSize: number): Promise<OutgoingEmailPage> {
    const params = new URLSearchParams({ status, page: String(page), pageSize: String(pageSize) })
    if (search) params.set('search', search)
    const { data } = await http.get<OutgoingEmailPage>(`/emails?${params}`)
    return data
  }

  /** Back in the queue from its first attempt. The server refuses one that is not failed, or a reset link. */
  static async retry(id: string): Promise<OutgoingEmail> {
    const { data } = await http.post<OutgoingEmail>(`/emails/${id}/retry`)
    return data
  }
}
