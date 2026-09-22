import { currentLanguage } from '@/config/i18n'

/**
 * One currency, as the backend assumes; formatted, never computed with.
 *
 * Formatted for the language being read (specs/021): 1.234,56 in Vietnamese and 1,234.56 in English
 * are the same number, and showing either to the wrong reader is a misread waiting to happen.
 */
export const money = (value: number) =>
  value.toLocaleString(currentLanguage(), { minimumFractionDigits: 2, maximumFractionDigits: 2 })
