import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AuthContext } from '@ecommerce/core/context/auth/useAuth'
import type { AuthState } from '@ecommerce/core/context/auth/types'
import { AppRoutes } from '.'

/** The console's addresses in the back office (specs/137): the storefront's /admin/x is /x here. */
function renderAt(path: string, roles: string[]) {
  window.history.pushState({}, '', path)
  const value = {
    user: { id: 'u1', email: 'mod@demo.test', firstName: 'Mai', lastName: 'T', roles, emailConfirmed: true },
    restoring: false, isSeller: false, isAdmin: roles.includes('Admin'), isStaff: true, hasBackOffice: true,
    signIn: async () => ({ setupRequired: false }), completeSignIn: async () => {}, signUp: async () => {},
    signOut: async () => {}, refreshSession: async () => true,
  } as AuthState
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <AuthContext.Provider value={value}>
      <QueryClientProvider client={client}>
        <AppRoutes />
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

afterEach(() => window.history.pushState({}, '', '/'))

describe('the back office routes (specs/137)', () => {
  it("opens a moderator on the console's home, which sends them to their own page", async () => {
    renderAt('/', ['Moderator'])
    await waitFor(() => expect(window.location.pathname).toBe('/moderation'))
  })

  it('answers an address it does not know with the home page', async () => {
    renderAt('/nowhere', ['Moderator'])
    await waitFor(() => expect(window.location.pathname).toBe('/moderation'))
  })
})
