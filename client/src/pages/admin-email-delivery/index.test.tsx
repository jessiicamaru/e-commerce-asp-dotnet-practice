import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { OutgoingEmails } from '@/services/outgoing-email'
import type { OutgoingEmail } from '@/services/outgoing-email/types'
import { refusal } from '@/test/refusal'
import { renderAsAdmin } from '@/test/render'
import { AdminEmailDeliveryPage } from '.'

const email = (over: Partial<OutgoingEmail> = {}): OutgoingEmail => ({
  id: 'e-1', recipientId: 'u-1', recipientEmail: 'lan@example.test', template: 'OrderPaid', language: 'en', status: 'Failed',
  attempts: 12, lastError: 'Connection refused', createdAt: '2026-09-27T08:00:00Z', nextAttemptAt: '2026-09-27T09:00:00Z',
  sentAt: null, canRetry: true, ...over,
})
const page = (...items: OutgoingEmail[]) => ({ items, page: 1, pageSize: 12, totalCount: items.length })

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminEmailDeliveryPage (specs/087)', () => {
  /** What needs a person first: an email nobody received, who it was for, and why. */
  it('opens on the failed emails, with who they were for and why', async () => {
    const list = vi.spyOn(OutgoingEmails, 'list').mockResolvedValue(page(email()))
    renderAsAdmin(<AdminEmailDeliveryPage />, '/admin/email-delivery')

    const row = (await screen.findByText('lan@example.test')).closest('li')!
    expect(list).toHaveBeenCalledWith('Failed', '', 1, 12)
    expect(within(row).getByText('Order paid')).toBeInTheDocument()
    expect(within(row).getByText('Connection refused')).toBeInTheDocument()
    expect(within(row).getByText(/12 attempts/)).toBeInTheDocument()
  })

  it('asks for another state, and for one person, when chosen', async () => {
    const list = vi.spyOn(OutgoingEmails, 'list').mockResolvedValue(page())
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailDeliveryPage />, '/admin/email-delivery')
    await screen.findByText('Nothing here.')

    await user.click(screen.getByRole('tab', { name: 'Sent' }))
    await waitFor(() => expect(list).toHaveBeenLastCalledWith('Sent', '', 1, 12))
    await user.type(screen.getByRole('textbox', { name: "Search by recipient's email" }), 'lan@{Enter}')

    await waitFor(() => expect(list).toHaveBeenLastCalledWith('Sent', 'lan@', 1, 12))
  })

  it('sends a failed email again and says so', async () => {
    vi.spyOn(OutgoingEmails, 'list').mockResolvedValue(page(email()))
    const retry = vi.spyOn(OutgoingEmails, 'retry').mockResolvedValue(email({ status: 'Pending', attempts: 0, lastError: null }))
    const success = vi.spyOn(toast, 'success')
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailDeliveryPage />, '/admin/email-delivery')

    await user.click(await screen.findByRole('button', { name: 'Send again' }))

    expect(retry).toHaveBeenCalledWith('e-1')
    await waitFor(() => expect(success).toHaveBeenCalledWith('Back in the queue - it goes out within a minute.'))
  })

  /** A reset link has expired by the time somebody looks: no button, and the reason instead. */
  it('offers no retry for a link that expires', async () => {
    vi.spyOn(OutgoingEmails, 'list').mockResolvedValue(page(email({ template: 'PasswordReset', canRetry: false })))
    renderAsAdmin(<AdminEmailDeliveryPage />, '/admin/email-delivery')

    expect(await screen.findByText('A link that expires - the person asks for a new one.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Send again' })).not.toBeInTheDocument()
  })

  it("shows the server's refusal in its words", async () => {
    vi.spyOn(OutgoingEmails, 'list').mockResolvedValue(page(email()))
    vi.spyOn(OutgoingEmails, 'retry').mockRejectedValue(refusal(409, 'This email is pending; only a failed one can be sent again.'))
    const user = userEvent.setup()
    renderAsAdmin(<AdminEmailDeliveryPage />, '/admin/email-delivery')

    await user.click(await screen.findByRole('button', { name: 'Send again' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('only a failed one can be sent again')
  })
})
