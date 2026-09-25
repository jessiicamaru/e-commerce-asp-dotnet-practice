import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Auth } from '@/services/auth'
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
})
