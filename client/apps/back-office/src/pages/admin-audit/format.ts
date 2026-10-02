/**
 * A diff value as a person reads it (specs/041): text as itself, nothing as a dash, anything else - a
 * number, a list, an object - as compact JSON. The server sends JSON values, not display strings, so
 * "1200000" the number and "1200000" the text stay distinguishable in the data even where they look alike.
 */
export function formatValue(value: unknown): string {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'string') return value
  return JSON.stringify(value)
}

/** `ProductCreated` → `Product created`: the action names are PascalCase identifiers, not sentences. */
export function humanise(action: string): string {
  const words = action.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}

/** How far back a period reaches, as the ISO time the server filters from; `all` reaches back forever. */
export const PERIODS = ['day', 'week', 'month', 'all'] as const
export type Period = (typeof PERIODS)[number]

export function periodStart(period: Period, now: Date = new Date()): string | undefined {
  const days = { day: 1, week: 7, month: 30, all: 0 }[period]
  return days === 0 ? undefined : new Date(now.getTime() - days * 86_400_000).toISOString()
}
