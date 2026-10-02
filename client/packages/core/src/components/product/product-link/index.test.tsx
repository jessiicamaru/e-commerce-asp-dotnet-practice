import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { setCurrentApp } from '@ecommerce/core/config/apps'
import { ProductLink } from '.'

afterEach(() => setCurrentApp('storefront'))

describe('ProductLink (specs/137)', () => {
  it('is an in-app link in the storefront', () => {
    render(<MemoryRouter><ProductLink productId="p1">Canon</ProductLink></MemoryRouter>)

    const link = screen.getByRole('link', { name: 'Canon' })
    expect(link).toHaveAttribute('href', '/products/p1')
    expect(link).not.toHaveAttribute('target')
  })

  it('opens the storefront in a new tab from the back office', () => {
    setCurrentApp('back-office')
    render(<MemoryRouter><ProductLink productId="p1">Canon</ProductLink></MemoryRouter>)

    const link = screen.getByRole('link', { name: 'Canon' })
    expect(link).toHaveAttribute('href', 'http://localhost:5173/products/p1')
    expect(link).toHaveAttribute('target', '_blank')
  })
})
