import { StrictMode } from 'react'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { Auth } from '@/services/auth'
import { refusal } from '@/test/refusal'
import { ConfirmEmailPage } from '.'

function open(path: string, signedIn = false) {
  const refreshSession = vi.fn().mockResolvedValue(true)
  const value = {
    user: signedIn ? { id: 'u1', email: 'lan@demo.test', firstName: 'Lan', lastName: 'P', roles: ['Customer'], emailConfirmed: false } : null,
    restoring: false, isSeller: false, isAdmin: false, isStaff: false,
    signIn: async () => {}, signUp: async () => {}, signOut: async () => {}, refreshSession,
  } as AuthState

  render(
    <StrictMode>
      <AuthContext.Provider value={value}>
        <MemoryRouter initialEntries={[path]}>
          <ConfirmEmailPage />
        </MemoryRouter>
      </AuthContext.Provider>
    </StrictMode>,
  )
  return { refreshSession }
}

describe('ConfirmEmailPage (specs/063)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })
  afterEach(() => vi.restoreAllMocks())

  /** The link works once - StrictMode runs the effect twice, and the second use must not be sent. */
  it('sends the token from the link once, and says it is done', async () => {
    const confirm = vi.spyOn(Auth, 'confirmEmail').mockResolvedValue()
    open('/confirm-email?token=abc-123_X')

    expect(await screen.findByRole('status')).toHaveTextContent(i18n.t('auth:confirm.done'))
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(confirm).toHaveBeenCalledWith('abc-123_X')
  })

  it('renews a signed-in session so the banner goes', async () => {
    vi.spyOn(Auth, 'confirmEmail').mockResolvedValue()
    const { refreshSession } = open('/confirm-email?token=abc', true)

    await screen.findByRole('status')
    expect(refreshSession).toHaveBeenCalled()
  })

  it('says the link is spent or wrong when the server refuses it', async () => {
    vi.spyOn(Auth, 'confirmEmail').mockRejectedValue(refusal(400, 'One or more validation errors occurred.', {
      errors: { Token: ['This link is invalid or has expired.'] },
    }))
    open('/confirm-email?token=used')

    expect(await screen.findByRole('alert')).toHaveTextContent(i18n.t('auth:confirm.invalid'))
  })

  it('sends nothing for a link with no token', () => {
    const confirm = vi.spyOn(Auth, 'confirmEmail').mockResolvedValue()
    open('/confirm-email')

    expect(screen.getByRole('alert')).toHaveTextContent(i18n.t('auth:confirm.invalid'))
    expect(confirm).not.toHaveBeenCalled()
  })
})
