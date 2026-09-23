import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'

/**
 * Renders inside everything a page expects, signed in as a seller: a fresh query client (retries off,
 * so a refusal is seen at once), a router, and an auth context holding the Seller role.
 *
 * Here and not in a test file, because importing from a `.test.tsx` runs that file's suites again
 * inside the importing one.
 */
export function renderAsSeller(children: ReactNode, path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const value = {
    user: { id: 's1', email: 'a@b.test', firstName: 'Mai', lastName: 'T', roles: ['Seller', 'Customer'] },
    restoring: false,
    isSeller: true,
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
