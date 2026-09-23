import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { ImageDropzone } from '.'
import { MAX_IMAGE_BYTES, imageProblem } from './image-problem'

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

const file = (type: string, size = 10) => new File([new Uint8Array(size)], 'x', { type })

describe('imageProblem', () => {
  it('accepts what Catalog accepts', () => {
    for (const type of ['image/jpeg', 'image/png', 'image/webp']) {
      expect(imageProblem({ type, size: 10 })).toBeNull()
    }
  })

  /** SVG is refused by the server (specs/019) - it can carry script. Refused here too, before the upload. */
  it('refuses SVG and anything that is not a photograph', () => {
    expect(imageProblem({ type: 'image/svg+xml', size: 10 })).toBe('type')
    expect(imageProblem({ type: 'application/pdf', size: 10 })).toBe('type')
  })

  it('refuses anything over 2 MB', () => {
    expect(imageProblem({ type: 'image/png', size: MAX_IMAGE_BYTES + 1 })).toBe('size')
    expect(imageProblem({ type: 'image/png', size: MAX_IMAGE_BYTES })).toBeNull()
  })
})

describe('ImageDropzone', () => {
  it('hands on a dropped photograph', () => {
    const onFile = vi.fn()
    render(<ImageDropzone label="Photograph" onFile={onFile} />)

    const png = file('image/png')
    fireEvent.drop(screen.getByRole('button', { name: 'Photograph' }), { dataTransfer: { files: [png] } })

    expect(onFile).toHaveBeenCalledWith(png)
  })

  it('says why a file was refused, and does not send it', () => {
    const onFile = vi.fn()
    render(<ImageDropzone label="Photograph" onFile={onFile} />)

    fireEvent.drop(screen.getByRole('button', { name: 'Photograph' }), {
      dataTransfer: { files: [file('image/svg+xml')] },
    })

    expect(onFile).not.toHaveBeenCalled()
    expect(screen.getByText(/not a JPG, PNG or WebP/i)).toBeInTheDocument()
  })

  /** A second photograph dropped while the first is still uploading would race it. */
  it('ignores a drop while it is uploading', () => {
    const onFile = vi.fn()
    render(<ImageDropzone label="Photograph" onFile={onFile} busy />)

    fireEvent.drop(screen.getByRole('button', { name: 'Photograph' }), { dataTransfer: { files: [file('image/png')] } })

    expect(onFile).not.toHaveBeenCalled()
  })
})
