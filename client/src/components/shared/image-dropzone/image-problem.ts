/** What Catalog accepts (specs/019). It decides from the file's BYTES, so this is courtesy, not a guard. */
export const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const
export const MAX_IMAGE_BYTES = 2 * 1024 * 1024

/**
 * Why a file would be refused, before sending it - or null when it is fine to send.
 *
 * <p>
 * The server checks again and is the one that decides: it reads the type from the bytes, never from
 * the name or the browser's guess, and refuses SVG. Checking here only spares somebody a round trip
 * and an upload bar for a 9 MB photo that was always going to be refused.
 * </p>
 */
export function imageProblem(file: Pick<File, 'type' | 'size'>): 'type' | 'size' | null {
  if (!(IMAGE_TYPES as readonly string[]).includes(file.type)) return 'type'
  if (file.size > MAX_IMAGE_BYTES) return 'size'
  return null
}
