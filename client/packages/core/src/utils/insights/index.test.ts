import { describe, expect, it } from 'vitest'
import { periodDays, periodRange } from '.'

describe('periodDays (specs/047, 082)', () => {
  /** Every day, not only the days that sold - which is what made one day of orders fill the chart. */
  it('lists every day from the first to the last, both included, oldest first', () => {
    expect(periodDays('2026-09-22', '2026-09-24')).toEqual(['2026-09-22', '2026-09-23', '2026-09-24'])
  })

  it('crosses a month end', () => {
    expect(periodDays('2026-09-29', '2026-10-01')).toEqual(['2026-09-29', '2026-09-30', '2026-10-01'])
  })

  it('gives one day for a period of one day, thirty for thirty', () => {
    expect(periodDays('2026-09-24', '2026-09-24')).toEqual(['2026-09-24'])
    expect(periodDays('2026-08-26', '2026-09-24')).toHaveLength(30)
  })

  /** What an Order from before specs/082 - no firstDay - falls back to: the dates of the instants asked for. */
  it('reads an ISO instant as its date', () => {
    expect(periodDays('2026-09-22T10:00:00.000Z', '2026-09-24T10:00:00.000Z')).toEqual(['2026-09-22', '2026-09-23', '2026-09-24'])
  })

  it('draws nothing for ends the wrong way round or unreadable, rather than throwing', () => {
    expect(periodDays('2026-09-24', '2026-09-22')).toEqual([])
    expect(periodDays('', '2026-09-22')).toEqual([])
  })
})

describe('periodRange (specs/068)', () => {
  /** Today and the N - 1 days before it: N days, which the server snaps to the shop's days. */
  it('starts N - 1 days before now and ends now', () => {
    const now = new Date('2026-09-25T10:00:00Z')

    expect(periodRange(7, now)).toEqual({ from: '2026-09-19T10:00:00.000Z', to: '2026-09-25T10:00:00.000Z' })
    expect(periodDays(periodRange(7, now).from, periodRange(7, now).to)).toHaveLength(7)
  })
})
