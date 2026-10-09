import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Product } from '@ecommerce/core/services/product'
import type { Product as ProductModel } from '@ecommerce/core/services/product/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { MAX_PHOTOS, ProductPhotosCard } from '.'

const product = {
  id: 'p1',
  name: 'Linen shirt',
  imageUrl: '/api/products/p1/image?v=1',
  photos: [
    { id: 'a', url: '/api/products/p1/photos/a?v=a' },
    { id: 'b', url: '/api/products/p1/photos/b?v=b' },
    { id: 'c', url: '/api/products/p1/photos/c?v=c' },
  ],
} as unknown as ProductModel

const png = (name: string) => new File(['png'], name, { type: 'image/png' })

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('ProductPhotosCard (specs/160)', () => {
  it('adds several photographs one at a time, in the order chosen, skipping what is not an image', async () => {
    const add = vi.spyOn(Product, 'addPhoto').mockResolvedValue(product)
    const user = userEvent.setup({ applyAccept: false })
    renderAsSeller(<ProductPhotosCard product={product} />)

    await user.upload(screen.getByTestId('add-photos'), [png('one.png'), new File(['x'], 'notes.txt', { type: 'text/plain' }), png('two.png')])

    await waitFor(() => expect(add).toHaveBeenCalledTimes(2))
    expect(add.mock.calls.map(([, file]) => file.name)).toEqual(['one.png', 'two.png'])
  })

  it('sends no more than the gallery has room for', async () => {
    const add = vi.spyOn(Product, 'addPhoto').mockResolvedValue(product)
    const user = userEvent.setup()
    renderAsSeller(<ProductPhotosCard product={product} />)

    // A cover and 3 photographs: room for 6 more.
    const room = MAX_PHOTOS - 4
    await user.upload(screen.getByTestId('add-photos'), Array.from({ length: room + 2 }, (_, i) => png(`${i}.png`)))

    await waitFor(() => expect(add).toHaveBeenCalledTimes(room))
  })

  it('moves a photograph by sending the whole new order', async () => {
    const reorder = vi.spyOn(Product, 'reorderPhotos').mockResolvedValue(product)
    const user = userEvent.setup()
    renderAsSeller(<ProductPhotosCard product={product} />)

    await user.click(screen.getByRole('button', { name: 'Move photograph 4 earlier' }))

    await waitFor(() => expect(reorder).toHaveBeenCalledWith('p1', ['a', 'c', 'b']))
    // The ends cannot move further.
    expect(screen.getByRole('button', { name: 'Move photograph 2 earlier' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Move photograph 4 later' })).toBeDisabled()
  })

  it('makes a photograph the cover and removes one by its id', async () => {
    const cover = vi.spyOn(Product, 'makeCover').mockResolvedValue(product)
    const remove = vi.spyOn(Product, 'removePhoto').mockResolvedValue()
    const user = userEvent.setup()
    renderAsSeller(<ProductPhotosCard product={product} />)

    await user.click(screen.getByRole('button', { name: 'Make photograph 3 the cover' }))
    await waitFor(() => expect(cover).toHaveBeenCalledWith('p1', 'b'))

    await user.click(screen.getByRole('button', { name: 'Remove photograph 2' }))
    await waitFor(() => expect(remove).toHaveBeenCalledWith('p1', 'a'))
  })

  it("shows the server's refusal in its words", async () => {
    vi.spyOn(Product, 'addPhoto').mockRejectedValue(refusal(409, 'A product has at most 10 photographs. Remove one first.'))
    const user = userEvent.setup()
    renderAsSeller(<ProductPhotosCard product={product} />)

    await user.upload(screen.getByTestId('add-photos'), png('one.png'))

    expect(await screen.findByText('A product has at most 10 photographs. Remove one first.')).toBeInTheDocument()
  })

  it('cannot add to a full gallery', () => {
    const full = {
      ...product,
      photos: Array.from({ length: MAX_PHOTOS - 1 }, (_, i) => ({ id: `${i}`, url: `/p/${i}` })),
    } as unknown as ProductModel
    renderAsSeller(<ProductPhotosCard product={full} />)

    expect(screen.getByRole('button', { name: /Add photographs/ })).toBeDisabled()
    expect(screen.getByText('10 of 10')).toBeInTheDocument()
  })
})
