const VISITOR_STORAGE_KEY = 'visitor-id'
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/**
 * A random id this browser keeps for itself (specs/086, #173), so opening a product page again today adds no view.
 * It names nobody: it is made here, never sent anywhere but the view counter, and a signed-in person is counted by
 * their token instead. Without storage there is none, and the server counts each view - the gateway limits how many.
 */
export function visitorId(): string | undefined {
  try {
    const stored = localStorage.getItem(VISITOR_STORAGE_KEY)
    if (stored && UUID.test(stored)) return stored
    const made = crypto.randomUUID()
    localStorage.setItem(VISITOR_STORAGE_KEY, made)
    return made
  } catch {
    // Private browsing, blocked site data: a fresh id per call would dedupe nothing, so send none.
    return undefined
  }
}
