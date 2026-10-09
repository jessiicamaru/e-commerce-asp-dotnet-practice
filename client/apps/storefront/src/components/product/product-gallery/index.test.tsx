import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { ProductGallery } from '.'

const product = {
  id: 'p1',
  name: 'Linen shirt',
  imageUrl: '/api/products/p1/image?v=1',
  photos: [
    { id: 'a', url: '/api/products/p1/photos/a?v=a' },
    { id: 'b', url: '/api/products/p1/photos/b?v=b' },
  ],
}

// The large photograph is the first image; the thumbnails sit inside the labelled list.
const large = () => screen.getAllByRole('img')[0]
const thumbnails = () => within(screen.getByRole('list', { name: 'Photographs of Linen shirt' })).getAllByRole('button')

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ProductGallery (specs/160)', () => {
  it('shows the cover large and every photograph as a thumbnail, the cover first', () => {
    render(<ProductGallery product={product} />)

    expect(large()).toHaveAttribute('src', product.imageUrl)
    expect(thumbnails().map((b) => b.getAttribute('aria-label'))).toEqual([
      'Show photograph 1 of 3',
      'Show photograph 2 of 3',
      'Show photograph 3 of 3',
    ])
    expect(thumbnails()[0]).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows the photograph a thumbnail was chosen for', async () => {
    const user = userEvent.setup()
    render(<ProductGallery product={product} />)

    await user.click(thumbnails()[2])

    expect(large()).toHaveAttribute('src', '/api/products/p1/photos/b?v=b')
    expect(thumbnails()[2]).toHaveAttribute('aria-pressed', 'true')
  })

  it("follows the chosen shape's own photograph, and a new shape forgets an earlier pick", async () => {
    const user = userEvent.setup()
    const { rerender } = render(<ProductGallery product={product} variantImageUrl="/api/variant/silver" />)
    expect(large()).toHaveAttribute('src', '/api/variant/silver')

    await user.click(thumbnails()[1])
    expect(large()).toHaveAttribute('src', '/api/products/p1/photos/a?v=a')

    rerender(<ProductGallery product={product} variantImageUrl="/api/variant/black" />)
    expect(large()).toHaveAttribute('src', '/api/variant/black')
  })

  it('draws no strip for a product with the cover alone', () => {
    render(<ProductGallery product={{ ...product, photos: [] }} />)

    expect(screen.queryByRole('list')).toBeNull()
    expect(large()).toHaveAttribute('src', product.imageUrl)
  })
})
