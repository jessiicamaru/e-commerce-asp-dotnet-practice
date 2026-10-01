import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ProductImage } from '.'

const camera = { id: 'p1', name: 'Canon EOS R50', imageUrl: '/api/products/p1/image?v=1' }

describe('ProductImage (specs/122, #245)', () => {
  it('is tinted while the photograph loads, and plain once it has', () => {
    render(<ProductImage product={camera} />)

    const image = screen.getByRole('img', { name: 'Canon EOS R50' })
    expect(image.className).toContain('animate-pulse')
    expect(image.className).toContain('bg-muted')

    fireEvent.load(image)

    expect(image.className).not.toContain('animate-pulse')
    expect(image.className).toContain('bg-card')
  })

  it('is tinted again while a different photograph loads', () => {
    const { rerender } = render(<ProductImage product={camera} />)
    fireEvent.load(screen.getByRole('img'))

    rerender(<ProductImage product={camera} imageUrl="/api/products/p1/variants/v2/image?v=1" />)

    expect(screen.getByRole('img').className).toContain('animate-pulse')
  })
})
