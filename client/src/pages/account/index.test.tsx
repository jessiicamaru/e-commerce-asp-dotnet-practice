import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { toast } from 'sonner'
import { Auth } from '@/services/auth'
import { MyData } from '@/services/my-data'
import type { MyDataService } from '@/services/my-data/types'
import type { AccountProfile } from '@/services/auth/types'
import { refusal } from '@/test/refusal'
import { renderAsCustomer } from '@/test/render'
import { AccountPage } from '.'

const lan: AccountProfile = { email: 'lan@demo.test', firstName: 'Lan', lastName: 'Pham', phone: null, emailConfirmed: true }
const label = (key: string) => i18n.t(`auth:account.${key}`)

describe('AccountPage (specs/064)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
    vi.spyOn(Auth, 'me').mockResolvedValue(lan)
  })
  afterEach(() => vi.restoreAllMocks())

  it('shows my details and sends what I changed', async () => {
    const update = vi.spyOn(Auth, 'updateMe').mockResolvedValue({ ...lan, firstName: 'Mai', phone: '0912' })
    const user = userEvent.setup()
    renderAsCustomer(<AccountPage />, '/account')

    const first = await screen.findByLabelText(label('firstName'))
    expect(first).toHaveValue('Lan')
    await user.clear(first)
    await user.type(first, 'Mai')
    await user.type(screen.getByLabelText(/Phone/), '0912')
    await user.click(screen.getByRole('button', { name: label('save') }))

    await waitFor(() => expect(update).toHaveBeenCalledWith({ firstName: 'Mai', lastName: 'Pham', phone: '0912' }))
  })

  it('shows a refusal of my details in the server\'s words', async () => {
    vi.spyOn(Auth, 'updateMe').mockRejectedValue(refusal(400, 'First name is required.'))
    const user = userEvent.setup()
    renderAsCustomer(<AccountPage />, '/account')

    await user.click(await screen.findByRole('button', { name: label('save') }))

    expect(await screen.findByText('First name is required.')).toBeInTheDocument()
  })

  it('changes my password with my current one', async () => {
    const change = vi.spyOn(Auth, 'changePassword').mockResolvedValue()
    const user = userEvent.setup()
    renderAsCustomer(<AccountPage />, '/account')

    await user.type(await screen.findByLabelText(label('currentPassword')), 'Old-Passw0rd')
    await user.type(screen.getByLabelText(label('newPassword')), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText(label('confirmPassword')), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: label('changePassword') }))

    await waitFor(() => expect(change).toHaveBeenCalledWith('Old-Passw0rd', 'N3w-Passw0rd!'))
  })

  it('sends nothing when the two new passwords differ', async () => {
    const change = vi.spyOn(Auth, 'changePassword').mockResolvedValue()
    const user = userEvent.setup()
    renderAsCustomer(<AccountPage />, '/account')

    await user.type(await screen.findByLabelText(label('currentPassword')), 'Old-Passw0rd')
    await user.type(screen.getByLabelText(label('newPassword')), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText(label('confirmPassword')), 'N3w-Passw0rd?')
    await user.click(screen.getByRole('button', { name: label('changePassword') }))

    expect(await screen.findByRole('alert')).toHaveTextContent(label('mismatch'))
    expect(change).not.toHaveBeenCalled()
  })

  it('says beside the field when my current password is wrong', async () => {
    vi.spyOn(Auth, 'changePassword').mockRejectedValue(refusal(400, 'One or more validation errors occurred.', {
      errors: { CurrentPassword: ['Your current password is not correct.'] },
    }))
    const user = userEvent.setup()
    renderAsCustomer(<AccountPage />, '/account')

    await user.type(await screen.findByLabelText(label('currentPassword')), 'not-it')
    await user.type(screen.getByLabelText(label('newPassword')), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText(label('confirmPassword')), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: label('changePassword') }))

    expect(await screen.findByText('Your current password is not correct.')).toBeInTheDocument()
  })

  it('says how long to wait after too many wrong passwords', async () => {
    vi.spyOn(Auth, 'changePassword').mockRejectedValue(refusal(429, 'Too many', { retryAfter: 240 }))
    const user = userEvent.setup()
    renderAsCustomer(<AccountPage />, '/account')

    await user.type(await screen.findByLabelText(label('currentPassword')), 'guess')
    await user.type(screen.getByLabelText(label('newPassword')), 'N3w-Passw0rd!')
    await user.type(screen.getByLabelText(label('confirmPassword')), 'N3w-Passw0rd!')
    await user.click(screen.getByRole('button', { name: label('changePassword') }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts. Try again in 4 minutes.')
  })

  describe('download my data (specs/111)', () => {
    /** What the browser was handed to save: the file's name and its parsed contents. */
    const saved = () => {
      const blobs: Blob[] = []
      vi.stubGlobal('URL', { ...URL, createObjectURL: (blob: Blob) => (blobs.push(blob), 'blob:x'), revokeObjectURL: () => {} })
      const names: string[] = []
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
        names.push(this.download)
      })
      return { names, file: async () => JSON.parse(await blobs[0].text()) }
    }
    const answer = (service: MyDataService) =>
      Promise.resolve({ service, exportedAt: '2026-10-01T00:00:00Z', sections: { rows: [{ from: service }] }, withheld: [] })
    afterEach(() => vi.unstubAllGlobals())

    it('asks all six services and saves one file with me and every answer in it', async () => {
      const asked = vi.spyOn(MyData, 'of').mockImplementation(answer)
      const done = vi.spyOn(toast, 'success').mockReturnValue('t')
      const { names, file } = saved()
      const user = userEvent.setup()
      renderAsCustomer(<AccountPage />, '/account')

      await user.click(await screen.findByRole('button', { name: label('myDataDownload') }))

      await waitFor(() => expect(done).toHaveBeenCalledWith(label('myDataDone')))
      expect(asked.mock.calls.map(([service]) => service).sort()).toEqual(['activity', 'cart', 'catalog', 'identity', 'order', 'payment'])
      expect(names).toHaveLength(1)
      expect(names[0]).toMatch(/^my-data-\d{4}-\d{2}-\d{2}\.json$/)
      const contents = await file()
      expect(contents.person).toEqual({ id: 'u1', email: 'a@b.test' })
      expect(contents.services.order.sections.rows).toEqual([{ from: 'order' }])
    })

    it('still saves what answered, marks the rest and says which part is missing', async () => {
      vi.spyOn(MyData, 'of').mockImplementation((service) => (service === 'payment' ? Promise.reject(refusal(503, 'down')) : answer(service)))
      const warned = vi.spyOn(toast, 'warning').mockReturnValue('t')
      const { file } = saved()
      const user = userEvent.setup()
      renderAsCustomer(<AccountPage />, '/account')

      await user.click(await screen.findByRole('button', { name: label('myDataDownload') }))

      await waitFor(() => expect(warned).toHaveBeenCalled())
      expect(warned.mock.calls[0][0]).toContain(i18n.t('auth:account.myDataServices.payment'))
      const contents = await file()
      expect(contents.services.payment).toEqual({ unavailable: true })
      expect(contents.services.identity.service).toBe('identity')
    })
  })

  describe('delete my account (specs/112)', () => {
    const confirmAndDelete = async (user: ReturnType<typeof userEvent.setup>) => {
      await user.type(await screen.findByLabelText(label('deletePassword')), 'Passw0rd!23')
      await user.click(screen.getByRole('button', { name: label('deleteStart') }))
      await user.click(await screen.findByRole('button', { name: label('deleteConfirm') }))
    }

    it('asks for confirmation first, then deletes with my password and says so', async () => {
      const remove = vi.spyOn(Auth, 'deleteMe').mockResolvedValue()
      const done = vi.spyOn(toast, 'success').mockReturnValue('t')
      const user = userEvent.setup()
      renderAsCustomer(<AccountPage />, '/account')

      await user.type(await screen.findByLabelText(label('deletePassword')), 'Passw0rd!23')
      await user.click(screen.getByRole('button', { name: label('deleteStart') }))
      expect(remove).not.toHaveBeenCalled()
      await user.click(await screen.findByRole('button', { name: label('deleteConfirm') }))

      await waitFor(() => expect(done).toHaveBeenCalledWith(label('deleted')))
      expect(remove).toHaveBeenCalledWith('Passw0rd!23')
    })

    it('keeping the account sends nothing', async () => {
      const remove = vi.spyOn(Auth, 'deleteMe').mockResolvedValue()
      const user = userEvent.setup()
      renderAsCustomer(<AccountPage />, '/account')

      await user.type(await screen.findByLabelText(label('deletePassword')), 'Passw0rd!23')
      await user.click(screen.getByRole('button', { name: label('deleteStart') }))
      await user.click(await screen.findByRole('button', { name: label('deleteCancel') }))

      expect(remove).not.toHaveBeenCalled()
    })

    it('lists what keeps the account open, in my language', async () => {
      vi.spyOn(Auth, 'deleteMe').mockRejectedValue(
        refusal(409, 'Business is open.', { code: 'AccountHasOpenBusiness', reasons: ['OpenOrders', 'OpenReturns'] }),
      )
      const user = userEvent.setup()
      renderAsCustomer(<AccountPage />, '/account')

      await confirmAndDelete(user)

      const alert = await screen.findByRole('alert')
      expect(alert).toHaveTextContent(label('deleteBlockers.OpenOrders'))
      expect(alert).toHaveTextContent(label('deleteBlockers.OpenReturns'))
    })

    it('shows a wrong password on its field', async () => {
      vi.spyOn(Auth, 'deleteMe').mockRejectedValue(
        refusal(400, 'One or more validation errors occurred.', { errors: { Password: ['Your current password is not correct.'] } }),
      )
      const user = userEvent.setup()
      renderAsCustomer(<AccountPage />, '/account')

      await confirmAndDelete(user)

      expect(await screen.findByText('Your current password is not correct.')).toBeInTheDocument()
      expect(screen.getByLabelText(label('deletePassword'))).toHaveAttribute('aria-invalid', 'true')
    })
  })
})
