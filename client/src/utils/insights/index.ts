/**
 * Every calendar day of the period, oldest first, as the server writes them (UTC `YYYY-MM-DD`), ending on
 * the day `to` falls on. The chart needs the days with NO revenue too - drawing only the days that had
 * some turned a single day of orders into one bar filling the whole chart.
 */
export function periodDays(to: string, count: number): string[] {
  const end = new Date(to)
  const last = Date.UTC(end.getUTCFullYear(), end.getUTCMonth(), end.getUTCDate())
  return Array.from({ length: count }, (_, i) => new Date(last - (count - 1 - i) * 86_400_000).toISOString().slice(0, 10))
}

/**
 * The period a "last N days" choice asks the server for: today and the N - 1 days before it, which is exactly
 * what the chart draws - the server counts whole UTC days, both ends included (specs/055). Asking from N x 24 h
 * ago touched one day more (#125).
 */
export function periodRange(days: number, now: Date = new Date()): { from: string; to: string } {
  return { from: new Date(now.getTime() - (days - 1) * 86_400_000).toISOString(), to: now.toISOString() }
}
