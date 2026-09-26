import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { EmailTemplates } from '@/services/email-template'
import type { EmailTemplate } from '@/services/email-template/types'
import { refusal } from '@/test/refusal'
import { renderAsAdmin } from '@/test/render'
import { AdminEmailsPage } from '.'

// ProseMirror needs layout jsdom does not have; the page's logic is what is under test here, the editor has its own.
vi.mock('@/components/shared/rich-text-editor', () => ({
  RichTextEditor: ({ id, value, onChange }: { id: string; value: string; onChange: (html: string) => void }) => (
    <textarea id={id} defaultValue={value} onChange={(event) => onChange(event.target.value)} />
  ),
}))

const email = (over: Partial<EmailTemplate> = {}): EmailTemplate => ({
  template: 'OrderPaid', language: 'vi', subject: 'Đơn hàng {order} đã được thanh toán', bodyHtml: '<p>Xin chào {name},</p>',
  isDefault: true, version: 0, updatedAt: null, updatedBy: null, placeholders: ['name', 'order', 'total', 'link'], required: [],
  ...over,
})
const all = (...overrides: EmailTemplate[]) => [
  email(),
  email({ language: 'en', subject: 'Your order {order} is paid', bodyHtml: '<p>Hi {name},</p>' }),
  email({ template: 'PasswordReset', subject: 'Đặt lại mật khẩu', placeholders: ['name', 'link'], required: ['link'] }),
  email({ template: 'PasswordReset', language: 'en', subject: 'Reset your password', placeholders: ['name', 'link'], required: ['link'] }),
  ...overrides,
]

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(EmailTemplates, 'versions').mockResolvedValue([])
})

describe('AdminEmailsPage (specs/077)', () => {
  it('opens on the first email in the first language, and says what a security email must keep', async () => {
    vi.spyOn(EmailTemplates, 'list').mockResolvedValue(all())
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailsPage />, '/admin/emails')

    expect(await screen.findByLabelText('Subject')).toHaveValue('Đơn hàng {order} đã được thanh toán')
    expect(screen.getByText('Built-in words')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()   // nothing changed yet

    await user.click(screen.getByRole('tab', { name: 'Password reset' }))
    await user.click(screen.getByRole('tab', { name: 'English' }))

    expect(await screen.findByLabelText('Subject')).toHaveValue('Reset your password')
    expect(screen.getByText('Must keep: {link}')).toBeInTheDocument()
  })

  it('saves the draft on top of the version it opened, placeholders inserted where the cursor is', async () => {
    vi.spyOn(EmailTemplates, 'list').mockResolvedValue([email({ isDefault: false, version: 3, updatedAt: '2026-09-26T08:00:00Z' })])
    const save = vi.spyOn(EmailTemplates, 'save').mockResolvedValue(email({ version: 4 }))
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailsPage />, '/admin/emails')

    const subject = await screen.findByLabelText('Subject')
    await user.clear(subject)
    await user.type(subject, 'Paid: ')
    await user.click(screen.getByRole('button', { name: 'Insert {order} in the subject' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(save).toHaveBeenCalledWith('OrderPaid', 'vi', { subject: 'Paid: {order}', bodyHtml: '<p>Xin chào {name},</p>' }, 3),
    )
  })

  /** A subject and a body can each break a rule: both are said, in the server's words. */
  it('lists every refusal the server gave', async () => {
    vi.spyOn(EmailTemplates, 'list').mockResolvedValue(all())
    vi.spyOn(EmailTemplates, 'save').mockRejectedValue(
      refusal(400, 'One or more validation errors occurred.', {
        errors: { Subject: ['{discount} is not something this email can fill in.'], BodyHtml: ['{voucher} is not something this email can fill in.'] },
      }),
    )
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailsPage />, '/admin/emails')

    await user.type(await screen.findByLabelText('Subject'), ' {discount}')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText('{discount} is not something this email can fill in.')).toBeInTheDocument()
    expect(within(alert).getByText('{voucher} is not something this email can fill in.')).toBeInTheDocument()
  })

  it('previews the server rendering in a sandbox, with its text version', async () => {
    vi.spyOn(EmailTemplates, 'list').mockResolvedValue(all())
    vi.spyOn(EmailTemplates, 'preview').mockResolvedValue({ subject: 'Đơn hàng 01a0dd2b đã được thanh toán', html: '<p>Xin chào Mai,</p>', text: 'Xin chào Mai,' })
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailsPage />, '/admin/emails')

    await user.click(await screen.findByRole('button', { name: 'Preview' }))

    const frame = await screen.findByTitle('Email preview')
    expect(frame).toHaveAttribute('sandbox', '')
    expect(frame).toHaveAttribute('srcdoc', '<p>Xin chào Mai,</p>')
    expect(screen.getByText('Đơn hàng 01a0dd2b đã được thanh toán')).toBeInTheDocument()
  })

  it('restores an earlier version on top of the current one', async () => {
    vi.spyOn(EmailTemplates, 'list').mockResolvedValue([email({ isDefault: false, version: 2, updatedAt: '2026-09-26T08:00:00Z' })])
    vi.spyOn(EmailTemplates, 'versions').mockResolvedValue([
      { version: 2, isDefault: false, subject: 'b', bodyHtml: '<p>b</p>', createdAt: '2026-09-26T08:00:00Z', createdBy: 'u1' },
      { version: 1, isDefault: false, subject: 'a', bodyHtml: '<p>a</p>', createdAt: '2026-09-25T08:00:00Z', createdBy: 'u1' },
    ])
    const restore = vi.spyOn(EmailTemplates, 'restore').mockResolvedValue(email({ version: 3 }))
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailsPage />, '/admin/emails')

    expect(await screen.findByText('Current')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Restore' }))

    await waitFor(() => expect(restore).toHaveBeenCalledWith('OrderPaid', 'vi', 1, 2))
  })

  /** A save gives the editor a new version and remounts it: the toast must not go with it (specs/080). */
  it('says it was saved even when the editor is gone by the time the save answers', async () => {
    vi.spyOn(EmailTemplates, 'list').mockResolvedValue([email({ isDefault: false, version: 3, updatedAt: '2026-09-26T08:00:00Z' })])
    let answer: (saved: EmailTemplate) => void = () => {}
    vi.spyOn(EmailTemplates, 'save').mockReturnValue(new Promise<EmailTemplate>((resolve) => (answer = resolve)))
    const told = vi.spyOn(toast, 'success').mockReturnValue('t')
    const user = userEvent.setup()
    const { unmount } = renderAsAdmin(<AdminEmailsPage />, '/admin/emails')

    await user.type(await screen.findByLabelText('Subject'), '!')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    unmount()
    answer(email({ version: 4 }))

    await waitFor(() => expect(told).toHaveBeenCalledWith('Saved. The next email says this.'))
  })
})
