import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState, SignInStep } from '@ecommerce/core/context/auth/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { SignInPage } from '.'

function renderSignIn(signIn: () => Promise<SignInStep>, completeSignIn: AuthState['completeSignIn'] = async () => {}) {
  const value = {
    user: null, restoring: false, isSeller: false, isAdmin: false, isStaff: false, hasBackOffice: false,
    signIn, completeSignIn, signUp: async () => {}, signOut: async () => {}, refreshSession: async () => true,
  } as AuthState

  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/sign-in']}>
        <Routes>
          <Route path="/sign-in" element={<SignInPage />} />
          <Route path="/" element={<p>home</p>} />
          <Route path="/account/two-factor" element={<p>set up two-factor</p>} />
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

describe('SignInPage, two-factor sign-in (specs/110)', () => {
  it('asks for the code after the right password, and sends it with the challenge', async () => {
    const complete = vi.fn(async () => {})
    renderSignIn(async () => ({ challenge: 'ch-1' }), complete)
    const user = userEvent.setup()

    await typePassword(user)
    await user.type(await screen.findByLabelText('Code'), '123 456')
    await user.click(screen.getByRole('button', { name: 'Verify' }))

    await waitFor(() => expect(complete).toHaveBeenCalledWith('ch-1', { code: '123456' }))
    expect(await screen.findByText('home')).toBeInTheDocument()
  })

  it('takes a recovery code instead, sent as one', async () => {
    const complete = vi.fn(async () => {})
    renderSignIn(async () => ({ challenge: 'ch-1' }), complete)
    const user = userEvent.setup()

    await typePassword(user)
    await user.click(await screen.findByRole('button', { name: 'Use a recovery code instead' }))
    await user.type(screen.getByLabelText('Recovery code'), 'ABCDE-FGHIJ')
    await user.click(screen.getByRole('button', { name: 'Verify' }))

    await waitFor(() => expect(complete).toHaveBeenCalledWith('ch-1', { recoveryCode: 'ABCDE-FGHIJ' }))
  })

  it('keeps the code step after a wrong code, and says so', async () => {
    renderSignIn(async () => ({ challenge: 'ch-1' }), async () => {
      throw refusal(400, 'One or more validation errors occurred.', { errors: { Code: ['The code is not right, or was already used.'] } })
    })
    const user = userEvent.setup()

    await typePassword(user)
    await user.type(await screen.findByLabelText('Code'), '000000')
    await user.click(screen.getByRole('button', { name: 'Verify' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('That code is not right, or was already used.')
    expect(screen.getByLabelText('Code')).toBeInTheDocument()
  })

  /** A dead challenge - too old, or five wrong codes - starts again from the password. */
  it('goes back to the password when the challenge died', async () => {
    renderSignIn(async () => ({ challenge: 'ch-1' }), async () => {
      throw refusal(400, 'One or more validation errors occurred.', { errors: { Challenge: ['Sign in again.'] } })
    })
    const user = userEvent.setup()

    await typePassword(user)
    await user.type(await screen.findByLabelText('Code'), '000000')
    await user.click(screen.getByRole('button', { name: 'Verify' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Sign in again.')
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('sends staff without two-factor sign-in to set it up', async () => {
    renderSignIn(async () => ({ setupRequired: true }))
    const user = userEvent.setup()

    await typePassword(user)

    expect(await screen.findByText('set up two-factor')).toBeInTheDocument()
  })
})
