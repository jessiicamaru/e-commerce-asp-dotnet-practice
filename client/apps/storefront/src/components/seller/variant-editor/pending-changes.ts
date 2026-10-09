/**
 * What a save would send: only the fields that differ from what the server has, and only ones that
 * parse. An untouched field is not re-sent, so saving the stock cannot also rewrite a price somebody
 * else changed a minute ago.
 */
export function pendingChanges(
  typed: { prices: Record<string, string>; onHand: string | undefined; lowStock?: string; compareAts?: Record<string, string> },
  current: {
    prices: Record<string, number | null>
    /** What each price is compared against, null for none (specs/161). */
    compareAts?: Record<string, number | null>
    onHand: number | null
    /** The variant's own line, or null when it follows the shop's default (specs/102). */
    lowStock?: number | null
  },
) {
  const prices = Object.entries(typed.prices)
    .filter(([, value]) => value.trim() !== '')
    .map(([currency, value]) => ({ currency, amount: Number(value.replace(/[\s,]/g, '')) }))
    .filter(({ currency, amount }) => Number.isFinite(amount) && amount !== current.prices[currency])

  const onHandNumber = typed.onHand === undefined || typed.onHand.trim() === '' ? null : Number(typed.onHand)
  const onHand =
    onHandNumber !== null && Number.isInteger(onHandNumber) && onHandNumber >= 0 && onHandNumber !== current.onHand
      ? onHandNumber
      : null

  // An emptied box clears a compare-at the price has; a typed amount sets one (specs/161). The server decides whether it
  // is above the price - after the prices above are saved, so a new price counts.
  const compareAts = Object.entries(typed.compareAts ?? {}).flatMap(([currency, value]) => {
    const had = current.compareAts?.[currency] ?? null
    if (value.trim() === '') return had === null ? [] : [{ currency, amount: null as number | null }]
    const amount = Number(value.replace(/[\s,]/g, ''))
    return Number.isFinite(amount) && amount !== had ? [{ currency, amount: amount as number | null }] : []
  })

  return { prices, onHand, lowStock: lowStockChange(typed.lowStock, current.lowStock ?? null), compareAts }
}

/**
 * The low-stock line to send, or undefined for no change (specs/102). An emptied box means "the shop's default" -
 * sent as null, and only when the variant has a line of its own to give up. 0 is a value: never warn.
 */
function lowStockChange(typed: string | undefined, own: number | null): { threshold: number | null } | undefined {
  if (typed === undefined) return undefined
  if (typed.trim() === '') return own === null ? undefined : { threshold: null }
  const value = Number(typed)
  if (!Number.isInteger(value) || value < 0 || value > 100_000 || value === own) return undefined
  return { threshold: value }
}

/**
 * An amount as it is shown in the price box: digits grouped by thin spaces, `15 490 000`, and a
 * decimal point kept as it is (`119.95`).
 *
 * Spaces, deliberately - not the reader's separators. In Vietnamese a dot groups thousands, so
 * "15.490.000" would be read back by `pendingChanges` as fifteen and a bit. A space means nothing to a
 * number in either language, so what is shown is exactly what is read back.
 */
export function groupDigits(amount: number): string {
  const [whole, fraction] = String(amount).split('.')
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ' ')
  return fraction ? `${grouped}.${fraction}` : grouped
}
