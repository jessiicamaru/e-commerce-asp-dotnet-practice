import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'

/**
 * Renders inside everything a page expects, signed in with these roles: a fresh query client (retries
 * off, so a refusal is seen at once), a router, and an auth context.
 *
 * Here and not in a test file, because importing from a `.test.tsx` runs that file's suites again
 * inside the importing one.
 */
function renderWithRoles(roles: string[], children: ReactNode, path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const value = {
    user: { id: 'u1', email: 'a@b.test', firstName: 'Mai', lastName: 'T', roles },
    restoring: false,
    isSeller: roles.includes('Seller'),
    isAdmin: roles.includes('Admin'),
    signIn: async () => {}, signUp: async () => {}, signOut: async () => {},
  } as AuthState

  return render(
    <AuthContext.Provider value={value}>
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

/** Signed in as a seller, who is a customer too (specs/027). */
export function renderAsSeller(children: ReactNode, path = '/') {
  return renderWithRoles(['Seller', 'Customer'], children, path)
}

/** Signed in as an administrator (specs/038). */
export function renderAsAdmin(children: ReactNode, path = '/') {
  return renderWithRoles(['Admin'], children, path)
}
