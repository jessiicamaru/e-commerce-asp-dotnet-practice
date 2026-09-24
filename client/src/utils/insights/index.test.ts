import { describe, expect, it } from 'vitest'
import { periodDays } from '.'

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
