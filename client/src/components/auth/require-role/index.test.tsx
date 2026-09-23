import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import type { User } from '@/services/auth/types'
import { RequireRole } from '.'

function aUser(roles: string[]): User {
  return { id: 'u1', email: 'a@b.test', firstName: 'Alice', lastName: 'N', roles }
}

function renderAt(state: Partial<AuthState>, role: string | string[] = 'Seller') {
  const value = {
    user: null,
    restoring: false,
    isSeller: false,
    isAdmin: false,
    isStaff: false,
    refreshSession: async () => true,
    signIn: async () => {},
    signUp: async () => {},
    signOut: async () => {},
    ...state,
  } as AuthState

  return render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/shop']}>
        <Routes>
          <Route path="/" element={<p>catalogue</p>} />
          <Route path="/sign-in" element={<p>sign in</p>} />
          <Route
            path="/shop"
            element={
              <RequireRole role={role}>
                <p>the shop</p>
              </RequireRole>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('RequireRole', () => {
  it('lets a seller through', () => {
    renderAt({ user: aUser(['Seller', 'Customer']), isSeller: true })

    expect(screen.getByText('the shop')).toBeInTheDocument()
  })

  it('sends a signed-out visitor to sign in, not to the catalogue', () => {
    renderAt({ user: null })

    expect(screen.getByText('sign in')).toBeInTheDocument()
    expect(screen.queryByText('the shop')).not.toBeInTheDocument()
  })

  // A customer who typed the address made a wrong turn. Answering "forbidden" would also confirm
  // the page is real, which is the same reason the server answers 404 for somebody else's product.
  it('sends a signed-in customer to the catalogue rather than showing a refusal', () => {
    renderAt({ user: aUser(['Customer']) })

    expect(screen.getByText('catalogue')).toBeInTheDocument()
    expect(screen.queryByText('the shop')).not.toBeInTheDocument()
  })

  // The trap: deciding before the silent refresh has answered would bounce a seller who IS one.
  it('decides nothing while the session is still being restored', () => {
    renderAt({ user: null, restoring: true })

    expect(screen.queryByText('sign in')).not.toBeInTheDocument()
    expect(screen.queryByText('the shop')).not.toBeInTheDocument()
  })

  // Holding a DIFFERENT role is not holding this one - the check is includes(role), not "has any".
  it('does not accept a role that merely exists', () => {
    renderAt({ user: aUser(['Admin', 'Customer']) })

    expect(screen.getByText('catalogue')).toBeInTheDocument()
  })

  /** The console is for an administrator OR a moderator (specs/043) - any one of the roles named. */
  it('lets through anybody holding any of several roles, and nobody holding none', () => {
    renderAt({ user: aUser(['Moderator', 'Customer']) }, ['Admin', 'Moderator'])
    expect(screen.getByText('the shop')).toBeInTheDocument()
  })

  it('still refuses a customer when several roles are named', () => {
    renderAt({ user: aUser(['Customer', 'Seller']) }, ['Admin', 'Moderator'])
    expect(screen.getByText('catalogue')).toBeInTheDocument()
  })
})
