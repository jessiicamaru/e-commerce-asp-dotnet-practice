/**
 * What a save would send: only the fields that differ from what the server has, and only ones that
 * parse. An untouched field is not re-sent, so saving the stock cannot also rewrite a price somebody
 * else changed a minute ago.
 */
export function pendingChanges(
  typed: { prices: Record<string, string>; onHand: string | undefined },
  current: { prices: Record<string, number | null>; onHand: number | null },
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

  return { prices, onHand }
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
