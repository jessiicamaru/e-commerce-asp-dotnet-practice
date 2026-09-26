/** Per language, the notice keys an administrator reworded and what they say now (specs/078). */
export type WordingOverrides = Record<string, Record<string, string>>

/** One key's version: an edit, or `isDefault` - back to the words bundled with the storefront. */
export interface WordingEntry {
  key: string
  language: string
  text: string | null
  isDefault: boolean
  version: number
  updatedAt: string
  updatedBy: string
}

export interface WordingKind {
  kind: string
  placeholders: string[]
}

export interface WordingOverview {
  kinds: WordingKind[]
  entries: WordingEntry[]
}
