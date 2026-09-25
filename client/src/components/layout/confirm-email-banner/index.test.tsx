import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { toast } from 'sonner'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { Auth } from '@/services/auth'
import { refusal } from '@/test/refusal'
import { ConfirmEmailBanner } from '.'

function renderFor(user: AuthState['user']) {
  const value = {
    user, restoring: false, isSeller: false, isAdmin: false, isStaff: false,
    signIn: async () => {}, signUp: async () => {}, signOut: async () => {}, refreshSession: async () => true,
  } as AuthState
  return render(
    <AuthContext.Provider value={value}>
      <ConfirmEmailBanner />
    </AuthContext.Provider>,
  )
}

const lan = (emailConfirmed: boolean) =>
  ({ id: 'u1', email: 'lan@demo.test', firstName: 'Lan', lastName: 'P', roles: ['Customer'], emailConfirmed })

describe('ConfirmEmailBanner (specs/063)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })
  afterEach(() => vi.restoreAllMocks())

  it('asks an unconfirmed person to confirm, naming the address', () => {
    renderFor(lan(false))

    expect(screen.getByRole('status')).toHaveTextContent('lan@demo.test')
  })

  it('says nothing to a confirmed person, or to nobody', () => {
    const { container } = renderFor(lan(true))
    expect(container).toBeEmptyDOMElement()

    renderFor(null)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('sends the link again', async () => {
    const resend = vi.spyOn(Auth, 'resendConfirmation').mockResolvedValue()
    const success = vi.spyOn(toast, 'success').mockReturnValue('t')
    renderFor(lan(false))

    await userEvent.setup().click(screen.getByRole('button', { name: i18n.t('auth:confirm.resend') }))

    expect(resend).toHaveBeenCalledTimes(1)
    expect(success).toHaveBeenCalledWith(i18n.t('auth:confirm.resent'))
  })

  it('says how long to wait when links were asked for too fast', async () => {
    vi.spyOn(Auth, 'resendConfirmation').mockRejectedValue(refusal(429, 'Too many', { retryAfter: 30 }))
    const error = vi.spyOn(toast, 'error').mockReturnValue('t')
    renderFor(lan(false))

    await userEvent.setup().click(screen.getByRole('button', { name: i18n.t('auth:confirm.resend') }))

    expect(error).toHaveBeenCalledWith('Too many attempts. Try again in 1 minute.')
  })
})
