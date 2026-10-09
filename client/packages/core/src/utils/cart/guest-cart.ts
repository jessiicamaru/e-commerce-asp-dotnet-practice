/**
 * A signed-out shopper's cart, kept in this browser (specs/162, #370): what was chosen and how many - never a price,
 * which the server reads from Catalog when the cart is shown (`Cart.price`). Merged into the account's cart at sign-in
 * and emptied.
 *
 * Every read and write is wrapped: storage can be full, blocked or missing (a private window). Then nothing is kept,
 * `add` says so, and adding sends the shopper to sign in as it always did (research D5).
 */

export interface GuestCartLine {
  productId: string
  variantId: string
  quantity: number
}

/** The server's bounds (research D4): one request's worth of lines, and a sane quantity. */
export const GUEST_CART_MAX_LINES = 50
export const GUEST_CART_MAX_QUANTITY = 999

const KEY = 'guestCart'
const listeners = new Set<() => void>()
let cached: { raw: string | null; lines: GuestCartLine[] } = { raw: null, lines: [] }

function isLine(value: unknown): value is GuestCartLine {
  const line = value as GuestCartLine
  return (
    typeof line === 'object' && line !== null
    && typeof line.productId === 'string' && line.productId !== ''
    && typeof line.variantId === 'string' && line.variantId !== ''
    && Number.isInteger(line.quantity) && line.quantity >= 1 && line.quantity <= GUEST_CART_MAX_QUANTITY
  )
}

/** The lines, or none: anything unreadable (edited, from an older version) is dropped rather than sent. */
export function readGuestCart(): GuestCartLine[] {
  let raw: string | null
  try {
    raw = localStorage.getItem(KEY)
  } catch {
    return []
  }
  // The same array while nothing changed, so a subscriber does not re-render for nothing.
  if (raw === cached.raw) return cached.lines
  let lines: GuestCartLine[] = []
  try {
    const parsed = raw ? (JSON.parse(raw) as { lines?: unknown }) : null
    lines = Array.isArray(parsed?.lines) ? parsed.lines.filter(isLine).slice(0, GUEST_CART_MAX_LINES) : []
  } catch {
    lines = []
  }
  cached = { raw, lines }
  return lines
}

function write(lines: GuestCartLine[]): boolean {
  try {
    if (lines.length === 0) localStorage.removeItem(KEY)
    else localStorage.setItem(KEY, JSON.stringify({ lines }))
  } catch {
    return false
  }
  listeners.forEach((listener) => listener())
  return true
}

/**
 * Adds a shape, or raises its quantity - one line per shape, as the server's cart keeps them (specs/020). False when it
 * could not be kept: storage unavailable, or a 51st line.
 */
export function addToGuestCart(productId: string, variantId: string, quantity: number): boolean {
  const lines = readGuestCart()
  const held = lines.find((line) => line.variantId === variantId)
  if (!held && lines.length >= GUEST_CART_MAX_LINES) return false
  const next = held
    ? lines.map((line) =>
        line === held ? { ...line, quantity: Math.min(line.quantity + quantity, GUEST_CART_MAX_QUANTITY) } : line,
      )
    : [...lines, { productId, variantId, quantity: Math.min(quantity, GUEST_CART_MAX_QUANTITY) }]
  return write(next)
}

export function setGuestCartQuantity(variantId: string, quantity: number): boolean {
  const lines = readGuestCart()
  return write(
    lines.map((line) =>
      line.variantId === variantId ? { ...line, quantity: Math.min(Math.max(quantity, 1), GUEST_CART_MAX_QUANTITY) } : line,
    ),
  )
}

export function removeFromGuestCart(variantId: string): boolean {
  return write(readGuestCart().filter((line) => line.variantId !== variantId))
}

export function clearGuestCart(): void {
  write([])
}

/** For `useSyncExternalStore`: this tab's changes, and another tab's through the `storage` event. */
export function subscribeToGuestCart(listener: () => void): () => void {
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
