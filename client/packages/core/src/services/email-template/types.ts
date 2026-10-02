/**
 * One email in one language as it currently reads (specs/077): an administrator's version, or the built-in words
 * when `isDefault`. `version` 0 means nobody ever saved one. The placeholders are what it may use; `required` what it
 * may not lose.
 */
export interface EmailTemplate {
  template: string
  language: string
  subject: string
  bodyHtml: string
  isDefault: boolean
  version: number
  updatedAt: string | null
  updatedBy: string | null
  placeholders: string[]
  required: string[]
}

export interface EmailTemplateVersion {
  version: number
  isDefault: boolean
  subject: string
  bodyHtml: string
  createdAt: string
  createdBy: string
}

/** A draft filled with made-up data, by the server: what a recipient would get. */
export interface EmailPreview {
  subject: string
  html: string
  text: string
}

export interface EmailDraft {
  subject: string
  bodyHtml: string
}
