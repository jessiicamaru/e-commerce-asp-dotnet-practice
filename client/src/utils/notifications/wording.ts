import type { i18n as I18n } from 'i18next'
import en from '@/locales/en/notifications.json'
import vi from '@/locales/vi/notifications.json'
import type { WordingOverrides } from '@/services/notification-wording/types'

/** The words each notice kind has in the bundle - the default under any edit, and what a reset goes back to. */
export const BUNDLED_WORDING: Record<string, Record<string, string>> = { en: en.kind, vi: vi.kind }

/**
 * Lays an administrator's edits over the bundled words (specs/078): the bundle first, then the edits - so a key
 * whose edit was reset since the last load goes back to the bundle rather than keeping a stale edit.
 */
export function applyWording(i18n: I18n, overrides: WordingOverrides) {
  for (const [language, bundled] of Object.entries(BUNDLED_WORDING)) {
    i18n.addResourceBundle(language, 'notifications', { kind: { ...bundled, ...(overrides[language] ?? {}) } }, true, true)
  }
}

/** A draft filled with made-up values, escaped like real ones - what the console shows beside the editor. */
export function fillSample(text: string, sample: Record<string, string>): string {
  return text.replace(/{{\s*(\w+)\s*}}/g, (whole, name: string) => sample[name] ?? whole)
}
