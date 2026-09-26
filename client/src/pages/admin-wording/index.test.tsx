import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { NotificationWording } from '@/services/notification-wording'
import type { WordingEntry, WordingOverview } from '@/services/notification-wording/types'
import { refusal } from '@/test/refusal'
import { renderAsAdmin } from '@/test/render'
import { AdminWordingPage } from '.'

// ProseMirror needs layout jsdom does not have; the page's logic is what is under test here.
vi.mock('@/components/shared/rich-text-editor', () => ({
  RichTextEditor: ({ id, value, onChange }: { id: string; value: string; onChange: (html: string) => void }) => (
    <textarea id={id} defaultValue={value} onChange={(event) => onChange(event.target.value)} />
  ),
}))

const overview = (over: Partial<WordingOverview> = {}): WordingOverview => ({
  kinds: [
    { kind: 'NewSale', placeholders: ['order'] },
    { kind: 'NewReview', placeholders: ['count', 'product', 'rating'] },
  ],
  entries: [],
  ...over,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(NotificationWording, 'versions').mockResolvedValue([])
})

describe('AdminWordingPage (specs/078)', () => {
  it("lists each kind's sentence in both languages, a plural form by form", async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(overview())
    renderAsAdmin(<AdminWordingPage />, '/admin/notifications')

    expect(await screen.findByRole('button', { name: 'Edit NewSale (en)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit NewSale (vi)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit NewReview_one (en)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit NewReview_other (en)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit NewReview (vi)' })).toBeInTheDocument()
    expect(screen.getByText('Bạn có đơn bán mới: {{order}}.')).toBeInTheDocument()
  })

  it('saves an edit on top of the version it opened, with a live sample', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(
      overview({ entries: [{ key: 'NewSale', language: 'en', text: 'Sold: {{order}}', isDefault: false, version: 2, updatedAt: '2026-09-26T08:00:00Z', updatedBy: 'u1' }] }),
    )
    const save = vi.spyOn(NotificationWording, 'save').mockResolvedValue({
      key: 'NewSale', language: 'en', text: 'x', isDefault: false, version: 3, updatedAt: '', updatedBy: 'u1',
    })
    const user = userEvent.setup()
    renderAsAdmin(<AdminWordingPage />, '/admin/notifications')

    expect(await screen.findByText('Edited')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Edit NewSale (en)' }))
    const words = screen.getByLabelText('Words')
    await user.clear(words)
    await user.type(words, 'A new sale: {{{{order}}')

    expect(screen.getByText('A new sale: 01a0dd2b')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(save).toHaveBeenCalledWith('NewSale', 'en', 'A new sale: {{order}}', 2))
  })

  it('shows the refusal that names the placeholder', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(overview())
    vi.spyOn(NotificationWording, 'save').mockRejectedValue(
      refusal(400, 'One or more validation errors occurred.', { errors: { Text: ['{{total}} is not something a NewSale notice can fill in.'] } }),
    )
    const user = userEvent.setup()
    renderAsAdmin(<AdminWordingPage />, '/admin/notifications')

    await user.click(await screen.findByRole('button', { name: 'Edit NewSale (en)' }))
    await user.type(screen.getByLabelText('Words'), ' {{{{total}}')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('{{total}} is not something a NewSale notice can fill in.')).toBeInTheDocument()
  })

  /** A save gives the editor a new version and remounts it: the toast must not go with it (specs/080). */
  it('says it was saved even when the editor is gone by the time the save answers', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(overview())
    let answer: (saved: WordingEntry) => void = () => {}
    vi.spyOn(NotificationWording, 'save').mockReturnValue(new Promise<WordingEntry>((resolve) => (answer = resolve)))
    const told = vi.spyOn(toast, 'success').mockReturnValue('t')
    const user = userEvent.setup()
    const { unmount } = renderAsAdmin(<AdminWordingPage />, '/admin/notifications')

    await user.click(await screen.findByRole('button', { name: 'Edit NewSale (en)' }))
    await user.type(screen.getByLabelText('Words'), '!')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    unmount()
    answer({ key: 'NewSale', language: 'en', text: 'x', isDefault: false, version: 1, updatedAt: '', updatedBy: 'u1' })

    await waitFor(() => expect(told).toHaveBeenCalledWith('Saved. Readers see it on their next load.'))
  })
})
