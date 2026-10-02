import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState } from '@ecommerce/core/context/auth/types'
import { Auth } from '@ecommerce/core/services/auth'
import type { AuthResponse } from '@ecommerce/core/services/auth/types'
import { AuthCallbackPage } from '.'

let counter = 0

function renderCallback(code: string | null, signedIn = false, completeSignIn: AuthState['completeSignIn'] = async () => {}) {
  window.history.replaceState(null, '', code ? `/auth/callback#code=${code}` : '/auth/callback')
  const value = {
    user: signedIn ? { id: 'u1', email: 'mod@demo.test', firstName: 'Mai', lastName: 'T', roles: ['Moderator'], emailConfirmed: true } : null,
    restoring: false, isSeller: false, isAdmin: false, isStaff: signedIn, hasBackOffice: signedIn,
    signIn: async () => ({ setupRequired: false }), completeSignIn, signUp: async () => {}, signOut: async () => {}, refreshSession: async () => true,
  } as AuthState
  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/auth/callback']}>
        <Routes>
          <Route path="/auth/callback" element={<AuthCallbackPage />} />
          <Route path="/sign-in" element={<p>the ordinary sign-in</p>} />
          <Route path="/" element={<p>the console</p>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

const challenge = (over: Partial<AuthResponse> = {}): AuthResponse => ({
  id: 'u1', email: 'mod@demo.test', firstName: '', lastName: '', roles: [], emailConfirmed: true, token: '',
  twoFactor: 'Required', challenge: 'ch-1', ...over,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
  counter += 1
})

afterEach(() => window.history.replaceState(null, '', '/'))

describe('the back office callback (specs/140)', () => {
  it('redeems the handoff once, takes it out of the address, and asks only for the code', async () => {
    const redeem = vi.spyOn(Auth, 'redeemHandoff').mockResolvedValue(challenge())
    const complete = vi.fn(async () => {})
    const code = `code-${counter}`
    renderCallback(code, false, complete)

    const field = await screen.findByLabelText('Code')
    expect(window.location.hash).toBe('')
    expect(redeem).toHaveBeenCalledTimes(1)
    expect(redeem).toHaveBeenCalledWith(code)
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument()

    const user = userEvent.setup()
    await user.type(field, '123456')
    await user.click(screen.getByRole('button', { name: 'Verify' }))
    await waitFor(() => expect(complete).toHaveBeenCalledWith('ch-1', { code: '123456' }))
    expect(await screen.findByText('the console')).toBeInTheDocument()
  })

  it('opens the ordinary sign-in when the handoff is refused', async () => {
    vi.spyOn(Auth, 'redeemHandoff').mockRejectedValue(new Error('400'))
    renderCallback(`code-${counter}`)

    expect(await screen.findByText('the ordinary sign-in')).toBeInTheDocument()
  })

  it('opens the ordinary sign-in with no code at all', async () => {
    const redeem = vi.spyOn(Auth, 'redeemHandoff')
    renderCallback(null)

    expect(await screen.findByText('the ordinary sign-in')).toBeInTheDocument()
    expect(redeem).not.toHaveBeenCalled()
  })

  it('goes straight to the console when the back office is already signed in, spending nothing', async () => {
    const redeem = vi.spyOn(Auth, 'redeemHandoff')
    renderCallback(`code-${counter}`, true)

    expect(await screen.findByText('the console')).toBeInTheDocument()
    expect(redeem).not.toHaveBeenCalled()
  })
})
