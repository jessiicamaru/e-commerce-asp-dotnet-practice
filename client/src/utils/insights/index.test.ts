import { describe, expect, it } from 'vitest'
import { periodDays, periodRange } from '.'

describe('periodDays (specs/047)', () => {
  /** Every day, not only the days that sold - which is what made one day of orders fill the chart. */
  it('lists every day of the period, oldest first, ending on the last one', () => {
    expect(periodDays('2026-09-24T15:30:00Z', 3)).toEqual(['2026-09-22', '2026-09-23', '2026-09-24'])
  })

  it('crosses a month end', () => {
    expect(periodDays('2026-10-01T00:10:00Z', 3)).toEqual(['2026-09-29', '2026-09-30', '2026-10-01'])
  })

  it('gives as many days as asked', () => {
    expect(periodDays('2026-09-24T00:00:00Z', 30)).toHaveLength(30)
  })
})

describe('periodRange (specs/068)', () => {
  /** Today and the N - 1 days before it - exactly the days `periodDays` draws. */
  it('starts N - 1 days before now and ends now', () => {
    const now = new Date('2026-09-25T10:00:00Z')

    expect(periodRange(7, now)).toEqual({ from: '2026-09-19T10:00:00.000Z', to: '2026-09-25T10:00:00.000Z' })
    expect(periodDays(periodRange(7, now).to, 7)[0]).toBe(periodRange(7, now).from.slice(0, 10))
  })
})
