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
