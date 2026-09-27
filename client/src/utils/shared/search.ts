/** One entry of a searchable list. */
export interface Choice {
  value: string
  label: string
  /** Shown under the label, and searched too - e.g. a country's code. */
  hint?: string
}

/**
 * Text with the Vietnamese accents taken off and lower-cased: "Máy ảnh Đà Nẵng" → "may anh da nang".
 *
 * `đ` is not a combining accent - it is its own letter - so it does not come off with NFD and is
 * replaced by hand. Without that, "da nang" would never find "Đà Nẵng".
 */
export function fold(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim()
}

/**
 * An address for a name - "Ống kính rời" → "ong-kinh-roi" - what a new category's slug is suggested as (specs/097).
 * Accents come off the way {@link fold} takes them off, so the address reads like the name.
 */
export function slugOf(name: string): string {
  return fold(name)
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 150)
}

/**
 * Whether `query` appears in `text`, ignoring accents and case - the same promise the catalogue's own
 * search makes through `unaccent` (specs/021), so a dropdown does not find less than the search box.
 */
export function looselyIncludes(text: string, query: string): boolean {
  return fold(text).includes(fold(query))
}
