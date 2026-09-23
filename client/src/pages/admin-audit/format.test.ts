import { describe, expect, it } from 'vitest'
import { formatValue, humanise, periodStart } from './format'

describe('audit formatting (specs/041)', () => {
  it('shows a value as a person reads it', () => {
    expect(formatValue('Canon R50')).toBe('Canon R50')
    expect(formatValue(null)).toBe('—')
    expect(formatValue(undefined)).toBe('—')
    expect(formatValue(1200000)).toBe('1200000')
    expect(formatValue({ VND: 1 })).toBe('{"VND":1}')
    expect(formatValue(false)).toBe('false')
  })

  it('turns an action name into words', () => {
    expect(humanise('ProductCreated')).toBe('Product created')
    expect(humanise('SignInRefused')).toBe('Sign in refused')
  })

  it('reaches back as far as the period says, and forever for all', () => {
    const now = new Date('2026-09-24T12:00:00Z')
    expect(periodStart('day', now)).toBe('2026-09-23T12:00:00.000Z')
    expect(periodStart('week', now)).toBe('2026-09-17T12:00:00.000Z')
    expect(periodStart('all', now)).toBeUndefined()
  })
})
