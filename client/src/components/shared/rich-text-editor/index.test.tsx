import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { RichTextEditor } from '.'

// ProseMirror measures the selection to scroll it into view; jsdom has no layout, so give it empty boxes.
beforeAll(() => {
  const empty = () => ({ x: 0, y: 0, width: 0, height: 0, top: 0, right: 0, bottom: 0, left: 0, toJSON: () => ({}) })
  Range.prototype.getBoundingClientRect = empty as never
  Range.prototype.getClientRects = (() => ({ length: 0, item: () => null, [Symbol.iterator]: [][Symbol.iterator] })) as never
  document.elementFromPoint = (() => null) as never
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('RichTextEditor (specs/077)', () => {
  it('starts from the given words and inserts a placeholder from the list', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    render(<RichTextEditor id="body" value="<p>Hi</p>" onChange={onChange} placeholders={['name', 'link']} />)

    expect(await screen.findByText('Hi')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: '{name}' }))

    await waitFor(() => expect(onChange).toHaveBeenLastCalledWith(expect.stringContaining('{name}')))
  })

  /** The toolbar says what is on where the cursor is - what the next words will be. */
  it('shows bold as on once pressed, and off again', async () => {
    const user = userEvent.setup()
    render(<RichTextEditor id="body" value="<p>Hi</p>" onChange={vi.fn()} placeholders={[]} />)
    const bold = await screen.findByRole('button', { name: 'Bold' })
    expect(bold).toHaveAttribute('aria-pressed', 'false')

    await user.click(bold)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Bold' })).toHaveAttribute('aria-pressed', 'true'))

    await user.click(screen.getByRole('button', { name: 'Bold' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Bold' })).toHaveAttribute('aria-pressed', 'false'))
  })
})
