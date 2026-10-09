/**
 * The whole percentage a price is below its compare-at (specs/161), **rounded down** - a reduction is never overstated -
 * or null when there is nothing to show: no compare-at, no price, or less than one percent off.
 */
export function percentOff(price: number | null | undefined, compareAt: number | null | undefined): number | null {
  if (price === null || price === undefined || compareAt === null || compareAt === undefined) return null
  if (!(compareAt > price)) return null
  const percent = Math.floor(((compareAt - price) / compareAt) * 100)
  return percent >= 1 ? percent : null
}
