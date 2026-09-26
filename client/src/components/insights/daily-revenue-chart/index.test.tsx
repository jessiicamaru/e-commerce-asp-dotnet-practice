import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import { DailyRevenueChart } from '.'

const days = ['2026-09-20', '2026-09-21', '2026-09-22', '2026-09-23']
const rows = [
  { day: '2026-09-21', currency: 'VND', revenue: 2_000_000, orders: 1 },
  { day: '2026-09-23', currency: 'VND', revenue: 8_000_000, orders: 3 },
  { day: '2026-09-23', currency: 'USD', revenue: 99, orders: 1 },
]

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('DailyRevenueChart (specs/047)', () => {
  /** A day with nothing sold is still a day on the axis - otherwise one busy day fills the whole chart. */
  it('draws one column per day of the period, empty days included', () => {
    render(<DailyRevenueChart days={days} rows={rows} currency="VND" />)

    const columns = screen.getAllByRole('listitem')
    expect(columns).toHaveLength(4)
    expect(columns[0]).toHaveAccessibleName(/Sep 20: no sales/)
    expect(columns[3]).toHaveAccessibleName(/Sep 23: ₫8,000,000 · 3 orders/)
  })

  it('scales bars to the busiest day and names it', () => {
    render(<DailyRevenueChart days={days} rows={rows} currency="VND" />)

    expect(screen.getByText('Highest day ₫8,000,000')).toBeInTheDocument()
    const bars = screen.getAllByRole('listitem').map((column) => column.querySelector('span'))
    expect(bars[0]).toBeNull()
    expect(bars[1]?.style.height).toBe('25%')
    expect(bars[3]?.style.height).toBe('100%')
  })

  /** Only the chosen currency's days: dollars never stand on the same scale as dong. */
  it('shows one currency at a time', () => {
    render(<DailyRevenueChart days={days} rows={rows} currency="USD" />)

    expect(screen.getByText('Highest day $99.00')).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')[1]).toHaveAccessibleName(/no sales/)
  })

  it('says the day, the amount and the orders on hover', async () => {
    render(<DailyRevenueChart days={days} rows={rows} currency="VND" />)

    await userEvent.hover(screen.getAllByRole('listitem')[1])

    expect(screen.getByRole('status')).toHaveTextContent('Sep 21: ₫2,000,000 · 1 order')
  })
})
