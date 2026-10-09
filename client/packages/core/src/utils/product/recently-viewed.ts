/**
 * The products this browser opened, most recent first (specs/163, #375) - ids only, never sent anywhere but to read the
 * products back. A convenience, not a record: per browser, and nothing is lost when storage is unavailable.
 */

export const RECENTLY_VIEWED_MAX = 12

const KEY = 'recentlyViewed'
const listeners = new Set<() => void>()
let cached: { raw: string | null; ids: string[] } = { raw: null, ids: [] }

export function readRecentlyViewed(): string[] {
  let raw: string | null
  try {
    raw = localStorage.getItem(KEY)
  } catch {
    return []
  }
  // The same array while nothing changed, so a subscriber does not re-render for nothing.
  if (raw === cached.raw) return cached.ids
  let ids: string[] = []
  try {
    const parsed = raw ? (JSON.parse(raw) as unknown) : []
    ids = Array.isArray(parsed)
      ? [...new Set(parsed.filter((id): id is string => typeof id === 'string' && id !== ''))].slice(0, RECENTLY_VIEWED_MAX)
      : []
  } catch {
    ids = []
  }
  cached = { raw, ids }
  return ids
}

/** Puts a product first, once - opening it again moves it to the front. */
export function rememberViewed(productId: string): void {
  const ids = [productId, ...readRecentlyViewed().filter((id) => id !== productId)].slice(0, RECENTLY_VIEWED_MAX)
  try {
    localStorage.setItem(KEY, JSON.stringify(ids))
  } catch {
    return
  }
  listeners.forEach((listener) => listener())
}

/** For `useSyncExternalStore`: this tab's changes, and another tab's through the `storage` event. */
export function subscribeToRecentlyViewed(listener: () => void): () => void {
  listeners.add(listener)
  const onStorage = (event: StorageEvent) => {
    if (event.key === KEY || event.key === null) listener()
  }
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}
