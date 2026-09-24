import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@/config/i18n'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import { refusal } from '@/test/refusal'
import { SignInPage } from '.'

/** Signs in with whatever the server answers, and returns what the page then says. */
async function signInAnswered(answer: unknown) {
  const value = {
    user: null, restoring: false, isSeller: false, isAdmin: false, isStaff: false,
    signIn: async () => { throw answer },
    signUp: async () => {}, signOut: async () => {}, refreshSession: async () => true,
  } as AuthState

  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter>
        <SignInPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
  const user = userEvent.setup()
  await user.type(screen.getByLabelText(i18n.t('auth:signIn.email')), 'lan@demo.test')
  await user.type(screen.getByLabelText(i18n.t('auth:signIn.password')), 'Right-Passw0rd')
  await user.click(screen.getByRole('button', { name: i18n.t('auth:signIn.submit') }))
  return (await screen.findByRole('alert')).textContent
}

const until = '2026-10-01T07:30:00Z'
const locked = refusal(403, `This account is locked until 2026-10-01 07:30 UTC: Spam in reviews`, {
  code: 'AccountLocked', until, reason: 'Spam in reviews',
})
const banned = refusal(403, 'This account is banned: Fraud', { code: 'AccountBanned', reason: 'Fraud' })

describe('SignInPage refusals (specs/049)', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en')
  })

  /** #120: this read "Signing in failed. Try again in a moment." - the reason was thrown away. */
  it('tells a locked person why and until when, in their own time', async () => {
    const said = await signInAnswered(locked)

    expect(said).toContain('Spam in reviews')
    expect(said).toContain(new Date(until).toLocaleString('en'))
    expect(said).not.toContain('UTC')
  })

  it('tells a locked person in Vietnamese too', async () => {
    await i18n.changeLanguage('vi')
    const said = await signInAnswered(locked)

    expect(said).toContain('bị khoá')
    expect(said).toContain(new Date(until).toLocaleString('vi'))
    expect(said).toContain('Spam in reviews')
  })

  it('tells a banned person why, in either language', async () => {
    expect(await signInAnswered(banned)).toBe('This account is banned: Fraud')
  })

  it('tells a banned person in Vietnamese too', async () => {
    await i18n.changeLanguage('vi')
    const said = await signInAnswered(banned)
    expect(said).toContain('bị cấm')
    expect(said).toContain('Fraud')
  })

  /** #28: a wrong password says nothing about the account - locked or not. */
  it('says only "wrong" for a wrong password', async () => {
    expect(await signInAnswered(refusal(401, 'Invalid email or password.'))).toBe(i18n.t('auth:signIn.wrong'))
  })

  it('shows the server sentence for a refusal it has no words for', async () => {
    expect(await signInAnswered(refusal(403, 'Sign-in is paused for maintenance.'))).toBe('Sign-in is paused for maintenance.')
  })

  it('falls back to the generic sentence for a failure with no sentence', async () => {
    expect(await signInAnswered(refusal(500))).toBe(i18n.t('auth:signIn.failed'))
  })

  it('falls back to the generic sentence when the server was not reached', async () => {
    expect(await signInAnswered(new Error('network'))).toBe(i18n.t('auth:signIn.failed'))
  })
})
