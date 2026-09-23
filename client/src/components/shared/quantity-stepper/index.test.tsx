import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { QuantityStepper } from '.'
import { clampQuantity } from './clamp'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('clampQuantity', () => {
  it('keeps a whole number between the bounds', () => {
    expect(clampQuantity('3')).toBe(3)
    expect(clampQuantity('0')).toBe(1)
    expect(clampQuantity('2.7')).toBe(2)
    expect(clampQuantity('50', 1, 6)).toBe(6)
  })

  /** Not a number is not 1: the field goes back to what it was rather than guessing. */
  it('refuses what is not a number instead of guessing one', () => {
    expect(clampQuantity('abc')).toBeNull()
    expect(clampQuantity('')).toBeNull()
  })
})

describe('QuantityStepper', () => {
  it('steps up and down', () => {
    const onChange = vi.fn()
    render(<QuantityStepper value={2} onChange={onChange} label="Quantity" />)

    fireEvent.click(screen.getByRole('button', { name: 'Increase quantity' }))
    fireEvent.click(screen.getByRole('button', { name: 'Decrease quantity' }))

    expect(onChange.mock.calls).toEqual([[3], [1]])
  })

  it('cannot go below one', () => {
    render(<QuantityStepper value={1} onChange={() => {}} label="Quantity" />)

    expect(screen.getByRole('button', { name: 'Decrease quantity' })).toBeDisabled()
  })

  /** `max` is Inventory's available count: adding more than there is would only be refused at checkout. */
  it('stops at what is available', () => {
    render(<QuantityStepper value={4} max={4} onChange={() => {}} label="Quantity" />)

    expect(screen.getByRole('button', { name: 'Increase quantity' })).toBeDisabled()
  })

  /** Typing "12" must not send "1" first: a cart would be re-priced twice, the first time wrongly. */
  it('sends a typed quantity once, when it is committed', () => {
    const onChange = vi.fn()
    render(<QuantityStepper value={1} onChange={onChange} label="Quantity" />)
    const box = screen.getByLabelText('Quantity')

    fireEvent.change(box, { target: { value: '1' } })
    fireEvent.change(box, { target: { value: '12' } })
    expect(onChange).not.toHaveBeenCalled()

    fireEvent.keyDown(box, { key: 'Enter' })
    expect(onChange).toHaveBeenCalledExactlyOnceWith(12)
  })
})
