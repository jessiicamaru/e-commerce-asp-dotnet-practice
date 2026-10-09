import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { CompareAt } from '.'
import { percentOff } from './percent-off'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('percentOff (specs/161)', () => {
  it('rounds down, never overstating a reduction', () => {
    expect(percentOff(990_000, 1_200_000)).toBe(17) // 17.5%
    expect(percentOff(50, 100)).toBe(50)
  })

  it('has nothing to say without a reduction of at least one percent', () => {
    expect(percentOff(990_000, null)).toBeNull()
    expect(percentOff(null, 1_200_000)).toBeNull()
    expect(percentOff(1_200_000, 1_200_000)).toBeNull()
    expect(percentOff(1_300_000, 1_200_000)).toBeNull()
    expect(percentOff(995, 1_000)).toBeNull() // 0.5%
  })
})

describe('CompareAt (specs/161)', () => {
  it('strikes the compare-at through, in the price\'s currency, with the percentage off', () => {
    render(<CompareAt price={39} compareAt={49} currency="USD" />)

    const struck = screen.getByLabelText('Was $49.00')
    expect(struck.tagName).toBe('S')
    expect(screen.getByText('-20%')).toBeInTheDocument()
  })

  it('draws nothing when the price is not reduced', () => {
    const { container } = render(<CompareAt price={49} compareAt={null} currency="USD" />)

    expect(container).toBeEmptyDOMElement()
  })
})
