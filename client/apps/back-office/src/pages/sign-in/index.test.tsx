import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState, SignInStep } from '@ecommerce/core/context/auth/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { SignInPage } from '.'

function renderSignIn(signIn: () => Promise<SignInStep>, completeSignIn: AuthState['completeSignIn'] = async () => {}, from?: string) {
  const value = {
    user: null, restoring: false, isSeller: false, isAdmin: false, isStaff: false,
    signIn, completeSignIn, signUp: async () => {}, signOut: async () => {}, refreshSession: async () => true,
  } as AuthState

  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={[{ pathname: '/sign-in', state: from ? { from } : null }]}>
        <Routes>
          <Route path="/sign-in" element={<SignInPage />} />
          <Route path="/" element={<p>home</p>} />
          <Route path="/reports" element={<p>reports</p>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

async function typePassword(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Email'), 'mod@demo.test')
  await user.type(screen.getByLabelText('Password'), 'Right-Passw0rd')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('the back office sign-in (specs/136)', () => {
  it('asks for the code after the password, and goes back where the person was', async () => {
    const complete = vi.fn(async () => {})
    renderSignIn(async () => ({ challenge: 'ch-1' }), complete, '/reports')
    const user = userEvent.setup()

    await typePassword(user)
    await user.type(await screen.findByLabelText('Code'), '123456')
    await user.click(screen.getByRole('button', { name: 'Verify' }))

    await waitFor(() => expect(complete).toHaveBeenCalledWith('ch-1', { code: '123456' }))
    expect(await screen.findByText('reports')).toBeInTheDocument()
  })

  it('refuses a wrong password in the storefront words, the same form', async () => {
    renderSignIn(async () => Promise.reject(refusal(401, 'Invalid credentials')))
    await typePassword(userEvent.setup())

    expect(await screen.findByText('That email and password do not match an account.')).toBeInTheDocument()
  })

  /** None of the storefront's extras: staff accounts are not made by sign-up, a password is reset on the storefront. */
  it('offers neither sign-up nor a forgotten password', () => {
    renderSignIn(async () => ({ setupRequired: false }))

    expect(screen.getByText('Back office')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /create/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /forgot/i })).not.toBeInTheDocument()
  })

  it('sends staff without two-factor sign-in to the guard, which says where to set it up', async () => {
    renderSignIn(async () => ({ setupRequired: true }))
    await typePassword(userEvent.setup())

    expect(await screen.findByText('home')).toBeInTheDocument()
  })
})
