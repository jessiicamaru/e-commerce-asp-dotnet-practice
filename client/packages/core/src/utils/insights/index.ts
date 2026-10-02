/**
 * Every calendar day from `first` to `last`, both included, oldest first, as `YYYY-MM-DD`. The chart needs the
 * days with NO revenue too - drawing only the days that had some turned a single day of orders into one bar
 * filling the whole chart.
 *
 * The ends are the SERVER's (`firstDay` / `lastDay`, specs/082): it counts the shop's days, in the shop's time
 * zone, and the browser's clock knows neither. An ISO instant is read as its date, which is what an Order older
 * than specs/082 - no `firstDay` - falls back to.
 */
export function periodDays(first: string, last: string): string[] {
  const start = Date.parse(first.slice(0, 10))
  const end = Date.parse(last.slice(0, 10))
  const count = Math.round((end - start) / 86_400_000) + 1
  if (!Number.isFinite(count) || count < 1) return []
  return Array.from({ length: count }, (_, i) => new Date(start + i * 86_400_000).toISOString().slice(0, 10))
}

/**
 * The period a "last N days" choice asks the server for: today and the N - 1 days before it. The server snaps
 * each end to the shop's day it falls on, both ends included (specs/055, 082), and says which days those were -
 * the chart draws exactly them. Asking from N x 24 h ago touched one day more (#125).
 */
export function periodRange(days: number, now: Date = new Date()): { from: string; to: string } {
  return { from: new Date(now.getTime() - (days - 1) * 86_400_000).toISOString(), to: now.toISOString() }
}
