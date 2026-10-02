import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState } from '@ecommerce/core/context/auth/types'
import type { User } from '@ecommerce/core/services/auth/types'
import { RequireStaff } from '.'

function renderAs(user: Partial<User> | null, signOut = vi.fn(async () => {})) {
  const roles = user?.roles ?? []
  const value = {
    user: user && { id: 'u1', email: 'lan@demo.test', firstName: 'Lan', lastName: 'P', roles, emailConfirmed: true, ...user },
    restoring: false,
    isSeller: roles.includes('Seller'),
    isAdmin: roles.includes('Admin'),
    isStaff: roles.includes('Admin') || roles.includes('Moderator'), hasBackOffice: roles.includes('Admin') || roles.includes('Moderator'),
    signIn: async () => ({ setupRequired: false }), completeSignIn: async () => {}, signUp: async () => {}, signOut, refreshSession: async () => true,
  } as AuthState

  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/reports']}>
        <Routes>
          <Route path="/sign-in" element={<p>sign in, back to {'/reports'}</p>} />
          <Route path="*" element={<RequireStaff><p>the console</p></RequireStaff>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
  return signOut
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('RequireStaff: who the back office lets in (specs/136)', () => {
  it('sends somebody signed out to sign in', () => {
    renderAs(null)
    expect(screen.getByText('sign in, back to /reports')).toBeInTheDocument()
    expect(screen.queryByText('the console')).not.toBeInTheDocument()
  })

  it.each([['Admin'], ['Moderator']])('lets a signed-in %s in', (role) => {
    renderAs({ roles: [role] })
    expect(screen.getByText('the console')).toBeInTheDocument()
  })

  it('tells a customer or a seller it is for staff, and offers only signing out', async () => {
    const signOut = renderAs({ roles: ['Seller', 'Customer'] })

    expect(screen.getByText('This is for staff')).toBeInTheDocument()
    expect(screen.queryByText('the console')).not.toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Sign out' }))
    expect(signOut).toHaveBeenCalled()
  })

  /** specs/110: without a code the server writes no staff role - the person is told where to set it up. */
  it('tells staff without two-factor sign-in to set it up on the storefront', () => {
    renderAs({ roles: ['Customer'], twoFactorSetupRequired: true })

    expect(screen.getByText('Set up two-factor sign-in first')).toBeInTheDocument()
    expect(screen.getByText(/from your account on the storefront/)).toBeInTheDocument()
    expect(screen.queryByText('the console')).not.toBeInTheDocument()
  })
})
