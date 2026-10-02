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

  /** specs/125 (#251): large, a photograph keeps its own shape once loaded - no square frame and no bands. */
  it('draws a large photograph at its own shape once loaded, on a 4:3 frame until then', () => {
    render(<ProductImage product={camera} large />)
    const image = screen.getByRole('img', { name: 'Canon EOS R50' })
    expect(image.className).toContain('aspect-[4/3]')

    fireEvent.load(image)

    expect(image.className).not.toMatch(/aspect-/)
    expect(image.className).toContain('h-auto')
    expect(image.className).toContain('max-h-')
  })

  it('keeps fixed frames for cards and for a large product with no photograph', () => {
    const { container, rerender } = render(<ProductImage product={camera} />)
    fireEvent.load(screen.getByRole('img'))
    expect(screen.getByRole('img').className).toContain('aspect-[4/3]')

    rerender(<ProductImage product={{ ...camera, imageUrl: null }} large />)
    expect((container.firstChild as HTMLElement).className).toContain('aspect-[4/3]')
  })

  it('is tinted again while a different photograph loads', () => {
    const { rerender } = render(<ProductImage product={camera} />)
    fireEvent.load(screen.getByRole('img'))

    rerender(<ProductImage product={camera} imageUrl="/api/products/p1/variants/v2/image?v=1" />)

    expect(screen.getByRole('img').className).toContain('animate-pulse')
  })
})
