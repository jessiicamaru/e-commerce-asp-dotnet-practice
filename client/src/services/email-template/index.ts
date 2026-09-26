// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { EmailDraft, EmailPreview, EmailTemplateVersion, EmailTemplate } from './types'

/**
 * The words of the shop's emails (specs/077, #150) - administrators only. The server sanitises what is saved, checks
 * its placeholders, and keeps every version; nothing here is trusted to have done either.
 */
export class EmailTemplates {
  static async list(): Promise<EmailTemplate[]> {
    const { data } = await http.get<EmailTemplate[]>('/email-templates')
    return data
  }

  static async versions(template: string, language: string): Promise<EmailTemplateVersion[]> {
    const { data } = await http.get<EmailTemplateVersion[]>(`/email-templates/${template}/${language}/versions`)
    return data
  }

  /** `expectedVersion` is the version the editor opened: somebody else's save since then is a 409. */
  static async save(template: string, language: string, draft: EmailDraft, expectedVersion: number): Promise<EmailTemplate> {
    const { data } = await http.put<EmailTemplate>(`/email-templates/${template}/${language}`, { ...draft, expectedVersion })
    return data
  }

  static async reset(template: string, language: string, expectedVersion: number): Promise<EmailTemplate> {
    const { data } = await http.post<EmailTemplate>(`/email-templates/${template}/${language}/reset`, { expectedVersion })
    return data
  }

  static async restore(template: string, language: string, version: number, expectedVersion: number): Promise<EmailTemplate> {
    const { data } = await http.post<EmailTemplate>(`/email-templates/${template}/${language}/versions/${version}/restore`, {
      expectedVersion,
    })
    return data
  }

  static async preview(template: string, language: string, draft: EmailDraft): Promise<EmailPreview> {
    const { data } = await http.post<EmailPreview>(`/email-templates/${template}/${language}/preview`, draft)
    return data
  }

  /** Sends the draft, filled with made-up data, to the caller's own address. */
  static async test(template: string, language: string, draft: EmailDraft): Promise<EmailPreview> {
    const { data } = await http.post<EmailPreview>(`/email-templates/${template}/${language}/test`, draft)
    return data
  }
}
