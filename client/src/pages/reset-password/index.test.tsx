import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Auth } from '@/services/auth'
import { refusal } from '@/test/refusal'
import { ResetPasswordPage } from '.'

function open(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <ResetPasswordPage />
    </MemoryRouter>,
  )
}

async function choose(password: string, confirm = password) {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText(i18n.t('auth:reset.password')), password)
  await user.type(screen.getByLabelText(i18n.t('auth:reset.confirm')), confirm)
  await user.click(screen.getByRole('button', { name: i18n.t('auth:reset.submit') }))
}

describe('ResetPasswordPage (specs/061)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })
  afterEach(() => vi.restoreAllMocks())

  it('sends the token from the link with the new password', async () => {
    const reset = vi.spyOn(Auth, 'resetPassword').mockResolvedValue()
    open('/reset-password?token=abc-123_X')

    await choose('N3w-Passw0rd!')

    expect(reset).toHaveBeenCalledWith('abc-123_X', 'N3w-Passw0rd!')
    expect(await screen.findByRole('status')).toHaveTextContent(i18n.t('auth:reset.done'))
    expect(screen.getByRole('link', { name: i18n.t('auth:reset.signIn') })).toHaveAttribute('href', '/sign-in')
  })

  it('sends nothing when the two passwords differ', async () => {
    const reset = vi.spyOn(Auth, 'resetPassword').mockResolvedValue()
    open('/reset-password?token=abc')

    await choose('N3w-Passw0rd!', 'N3w-Passw0rd?')

    expect(await screen.findByRole('alert')).toHaveTextContent(i18n.t('auth:reset.mismatch'))
    expect(reset).not.toHaveBeenCalled()
  })

  /** Used, expired, replaced or never issued - the server says one thing, and so does the page. */
  it('offers a new link when the server refuses the token', async () => {
    vi.spyOn(Auth, 'resetPassword').mockRejectedValue(
      refusal(400, 'One or more validation errors occurred.', {
        errors: { Token: ['This link is invalid or has expired. Ask for a new one.'] },
      }),
    )
    open('/reset-password?token=used')

    await choose('N3w-Passw0rd!')

    expect(await screen.findByRole('alert')).toHaveTextContent(i18n.t('auth:reset.invalidLink'))
    expect(screen.getByRole('link', { name: i18n.t('auth:reset.askAgain') })).toHaveAttribute('href', '/forgot-password')
  })

  it('shows the password rule the server applied, beside the field', async () => {
    vi.spyOn(Auth, 'resetPassword').mockRejectedValue(
      refusal(400, 'One or more validation errors occurred.', {
        errors: { Password: ['Password must be at least 8 characters.'] },
      }),
    )
    open('/reset-password?token=abc')

    await choose('short')

    expect(await screen.findByText('Password must be at least 8 characters.')).toBeInTheDocument()
  })

  it('says the link is broken when it carries no token, and offers no form', () => {
    open('/reset-password')

    expect(screen.getByRole('alert')).toHaveTextContent(i18n.t('auth:reset.invalidLink'))
    expect(screen.queryByLabelText(i18n.t('auth:reset.password'))).toBeNull()
  })
})
