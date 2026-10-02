import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { NotificationWording } from '@ecommerce/core/services/notification-wording'
import type { WordingEntry, WordingOverview } from '@ecommerce/core/services/notification-wording/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsAdmin } from '@ecommerce/core/test/render'
import declared from '../../../../../../server/src/BuildingBlocks/Ecommerce.Shared/Notifications/notification-kinds.json'
import { AdminWordingPage } from '.'

// ProseMirror needs layout jsdom does not have; the page's logic is what is under test here.
vi.mock('@ecommerce/core/components/shared/rich-text-editor', () => ({
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
  /** specs/128 (#250): one kind, one language at a time - chosen by name, kept in the address. */
  it('shows the chosen kind in the chosen language, a plural form by form, and keeps the choice in the address', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(overview())
    const user = userEvent.setup()
    renderAsAdmin(<AdminWordingPage />, '/notifications')

    // The first kind, in the reader's language.
    expect(await screen.findByRole('button', { name: 'Edit NewSale (en)' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit NewSale (vi)' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Edit NewReview/ })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'New review' }))
    expect(screen.getByRole('button', { name: 'Edit NewReview_one (en)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit NewReview_other (en)' })).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: /Tiếng Việt|vi/ }))
    expect(screen.getByRole('button', { name: 'Edit NewReview (vi)' })).toBeInTheDocument()
  })

  it('opens on the kind and language the address names', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(overview())
    renderAsAdmin(<AdminWordingPage />, '/notifications?kind=NewSale&lang=vi')

    expect(await screen.findByText('Bạn có đơn bán mới: {{order}}.')).toBeInTheDocument()
  })

  it('finds a kind by its name', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(overview())
    const user = userEvent.setup()
    renderAsAdmin(<AdminWordingPage />, '/notifications')

    await user.type(await screen.findByRole('textbox', { name: 'Find a notice' }), 'new r')

    const kinds = within(screen.getByRole('navigation', { name: 'Notices' })).getAllByRole('button')
    expect(kinds.map((k) => k.textContent)).toEqual(['New review'])
  })

  it('has a name for every kind a service can send, in both languages', () => {
    for (const kind of Object.keys(declared.kinds)) {
      for (const language of ['en', 'vi']) {
        expect(i18n.exists(`admin:wording.kindName.${kind}`, { lng: language }), `${kind} in ${language}`).toBe(true)
      }
    }
  })

  it('saves an edit on top of the version it opened, with a live sample', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(
      overview({ entries: [{ key: 'NewSale', language: 'en', text: 'Sold: {{order}}', isDefault: false, version: 2, updatedAt: '2026-09-26T08:00:00Z', updatedBy: 'u1' }] }),
    )
    const save = vi.spyOn(NotificationWording, 'save').mockResolvedValue({
      key: 'NewSale', language: 'en', text: 'x', isDefault: false, version: 3, updatedAt: '', updatedBy: 'u1',
    })
    const user = userEvent.setup()
    renderAsAdmin(<AdminWordingPage />, '/notifications')

    // Marked in the list of kinds and on the sentence itself.
    expect(await screen.findAllByText('Edited')).toHaveLength(2)
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
    renderAsAdmin(<AdminWordingPage />, '/notifications')

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
    const { unmount } = renderAsAdmin(<AdminWordingPage />, '/notifications')

    await user.click(await screen.findByRole('button', { name: 'Edit NewSale (en)' }))
    await user.type(screen.getByLabelText('Words'), '!')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    unmount()
    answer({ key: 'NewSale', language: 'en', text: 'x', isDefault: false, version: 1, updatedAt: '', updatedBy: 'u1' })

    await waitFor(() => expect(told).toHaveBeenCalledWith('Saved. Readers see it on their next load.'))
  })

  /** The same remount after a reset and a restore (#186, specs/094) - specs/080 fixed all three, but only save was held. */
  it('says it was reset even when the editor is gone by the time the reset answers', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(
      overview({ entries: [{ key: 'NewSale', language: 'en', text: 'Sold: {{order}}', isDefault: false, version: 2, updatedAt: '2026-09-26T08:00:00Z', updatedBy: 'u1' }] }),
    )
    let answer: (reset: WordingEntry) => void = () => {}
    vi.spyOn(NotificationWording, 'reset').mockReturnValue(new Promise<WordingEntry>((resolve) => (answer = resolve)))
    const told = vi.spyOn(toast, 'success').mockReturnValue('t')
    const user = userEvent.setup()
    const { unmount } = renderAsAdmin(<AdminWordingPage />, '/notifications')

    await user.click(await screen.findByRole('button', { name: 'Edit NewSale (en)' }))
    await user.click(screen.getByRole('button', { name: "Back to the storefront's words" }))
    unmount()
    answer({ key: 'NewSale', language: 'en', text: null, isDefault: true, version: 3, updatedAt: '', updatedBy: 'u1' })

    await waitFor(() => expect(told).toHaveBeenCalledWith("Back to the storefront's own words."))
  })

  it('says it was restored even when the editor is gone by the time the restore answers', async () => {
    vi.spyOn(NotificationWording, 'overview').mockResolvedValue(
      overview({ entries: [{ key: 'NewSale', language: 'en', text: 'Sold: {{order}}', isDefault: false, version: 2, updatedAt: '2026-09-26T08:00:00Z', updatedBy: 'u1' }] }),
    )
    vi.spyOn(NotificationWording, 'versions').mockResolvedValue([
      { key: 'NewSale', language: 'en', text: 'Sold: {{order}}', isDefault: false, version: 2, updatedAt: '2026-09-26T08:00:00Z', updatedBy: 'u1' },
      { key: 'NewSale', language: 'en', text: 'A sale: {{order}}', isDefault: false, version: 1, updatedAt: '2026-09-25T08:00:00Z', updatedBy: 'u1' },
    ])
    let answer: (restored: WordingEntry) => void = () => {}
    vi.spyOn(NotificationWording, 'restore').mockReturnValue(new Promise<WordingEntry>((resolve) => (answer = resolve)))
    const told = vi.spyOn(toast, 'success').mockReturnValue('t')
    const user = userEvent.setup()
    const { unmount } = renderAsAdmin(<AdminWordingPage />, '/notifications')

    await user.click(await screen.findByRole('button', { name: 'Edit NewSale (en)' }))
    await user.click(await screen.findByRole('button', { name: 'Restore' }))
    unmount()
    answer({ key: 'NewSale', language: 'en', text: 'A sale: {{order}}', isDefault: false, version: 3, updatedAt: '', updatedBy: 'u1' })

    await waitFor(() => expect(told).toHaveBeenCalledWith('Version 1 restored.'))
  })
})
