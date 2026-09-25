import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { refusal } from '@/test/refusal'
import { SignUpPage } from '.'

/** Fills the form, sends it, and returns what the page then says. */
async function signUpAnswered(signUp: AuthState['signUp']) {
  const value = {
    user: null, restoring: false, isSeller: false, isAdmin: false, isStaff: false,
    signIn: async () => {}, signUp, signOut: async () => {}, refreshSession: async () => true,
  } as AuthState

  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter>
        <SignUpPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
  const user = userEvent.setup()
  await user.type(screen.getByLabelText(i18n.t('auth:signUp.firstName')), 'Lan')
  await user.type(screen.getByLabelText(i18n.t('auth:signUp.lastName')), 'Pham')
  await user.type(screen.getByLabelText(i18n.t('auth:signUp.email')), 'lan@demo.test')
  await user.type(screen.getByLabelText(i18n.t('auth:signUp.password')), 'Passw0rd!23')
  await user.click(screen.getByRole('button', { name: i18n.t('auth:signUp.submit') }))
  return (await screen.findByRole('alert')).textContent
}

describe('SignUpPage refusals', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('sends what was typed', async () => {
    const signUp = vi.fn().mockRejectedValue(refusal(409))
    await signUpAnswered(signUp)

    expect(signUp).toHaveBeenCalledWith({ firstName: 'Lan', lastName: 'Pham', email: 'lan@demo.test', password: 'Passw0rd!23' })
  })

  it('says the address is taken', async () => {
    expect(await signUpAnswered(vi.fn().mockRejectedValue(refusal(409)))).toBe(i18n.t('auth:signUp.taken'))
  })

  /** specs/062: the gateway's per-client limit covers registration. */
  it('says how long to wait after too many attempts', async () => {
    expect(await signUpAnswered(vi.fn().mockRejectedValue(refusal(429, 'Too many', { retryAfter: 30 }))))
      .toBe('Too many attempts. Try again in 1 minute.')
  })

  it('says it in Vietnamese too', async () => {
    await i18n.changeLanguage('vi')
    expect(await signUpAnswered(vi.fn().mockRejectedValue(refusal(429, 'Too many', { retryAfter: 600 }))))
      .toBe('Quá nhiều lần thử. Thử lại sau 10 phút.')
  })
})
