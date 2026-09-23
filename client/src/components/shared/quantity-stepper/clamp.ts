/**
 * What a typed quantity means: a whole number between `min` and `max`, or null when it is not a
 * number at all - in which case the field goes back to what it was rather than guessing.
 */
export function clampQuantity(raw: string | number, min = 1, max?: number): number | null {
  const parsed = typeof raw === 'number' ? raw : Number(raw.trim())
  if (!Number.isFinite(parsed) || (typeof raw === 'string' && raw.trim() === '')) return null
  const whole = Math.floor(parsed)
  return Math.max(min, max === undefined ? whole : Math.min(max, whole))
}
