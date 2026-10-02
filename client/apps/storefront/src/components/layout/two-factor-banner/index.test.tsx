import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState } from '@ecommerce/core/context/auth/types'
import type { User } from '@ecommerce/core/services/auth/types'
import { TwoFactorBanner } from '.'

function renderBanner(user: User | null, path = '/') {
  const value = {
    user, restoring: false, isSeller: false, isAdmin: false, isStaff: false, hasBackOffice: false,
    signIn: async () => ({ setupRequired: false }), completeSignIn: async () => {}, signUp: async () => {}, signOut: async () => {},
    refreshSession: async () => true,
  } as AuthState
  return render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={[path]}>
        <TwoFactorBanner />
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

const mod: User = { id: 'u1', email: 'mod@b.test', firstName: 'Mai', lastName: 'T', roles: ['Customer'], emailConfirmed: true }

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('TwoFactorBanner (specs/110)', () => {
  it('sends staff without two-factor sign-in to set it up', () => {
    renderBanner({ ...mod, twoFactorSetupRequired: true })

    expect(screen.getByRole('status')).toHaveTextContent('needs two-factor sign-in')
    expect(screen.getByRole('link', { name: 'Set it up' })).toHaveAttribute('href', '/account/two-factor')
  })

  it('says nothing to anybody else, or on the setup page itself', () => {
    const { unmount } = renderBanner(mod)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    unmount()

    renderBanner({ ...mod, twoFactorSetupRequired: true }, '/account/two-factor')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})
