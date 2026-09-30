import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Auth } from '@/services/auth'
import type { AuthResponse } from '@/services/auth/types'
import type { AuthState } from './types'
import { useAuth } from './useAuth'
import { AuthProvider } from '.'

let auth: AuthState

/** Shows who is signed in, and hands the test the context to call - after render, as an effect. */
function Probe({ expose }: { expose: (value: AuthState) => void }) {
  const value = useAuth()
  useEffect(() => {
    expose(value)
  })
  const { user } = value
  return <p data-testid="who">{user ? `${user.email} ${user.roles.join(',')} ${user.twoFactorSetupRequired}` : 'nobody'}</p>
}

function renderProvider() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <AuthProvider>
        <Probe expose={(value) => (auth = value)} />
      </AuthProvider>
    </QueryClientProvider>,
  )
}

const session = (over: Partial<AuthResponse> = {}): AuthResponse => ({
  id: 'u1', email: 'mod@b.test', firstName: 'Mai', lastName: 'T', roles: ['Customer', 'Moderator'], emailConfirmed: true, token: 'jwt', ...over,
})

beforeEach(() => {
  // Nobody is signed in when the page loads.
  vi.spyOn(Auth, 'refresh').mockRejectedValue(new Error('no session'))
})

describe('AuthProvider, two-factor sign-in (specs/110)', () => {
  it('signs nobody in on the right password alone when a code is to follow', async () => {
    vi.spyOn(Auth, 'signIn').mockResolvedValue(session({ token: '', roles: [], twoFactor: 'Required', challenge: 'ch-1' }))
    renderProvider()
    await waitFor(() => expect(screen.getByTestId('who')).toHaveTextContent('nobody'))

    let step: unknown
    await act(async () => { step = await auth.signIn('mod@b.test', 'pw') })

    expect(step).toEqual({ challenge: 'ch-1' })
    expect(screen.getByTestId('who')).toHaveTextContent('nobody')
  })

  it('signs in with the code, and with the roles the answer carries', async () => {
    vi.spyOn(Auth, 'signInTwoFactor').mockResolvedValue(session())
    renderProvider()
    await waitFor(() => expect(screen.getByTestId('who')).toHaveTextContent('nobody'))

    await act(async () => { await auth.completeSignIn('ch-1', { code: '123456' }) })

    expect(Auth.signInTwoFactor).toHaveBeenCalledWith('ch-1', { code: '123456' })
    expect(screen.getByTestId('who')).toHaveTextContent('mod@b.test Customer,Moderator false')
  })

  it('remembers that staff must set it up', async () => {
    vi.spyOn(Auth, 'signIn').mockResolvedValue(session({ roles: ['Customer'], twoFactor: 'SetupRequired' }))
    renderProvider()
    await waitFor(() => expect(screen.getByTestId('who')).toHaveTextContent('nobody'))

    let step: unknown
    await act(async () => { step = await auth.signIn('mod@b.test', 'pw') })

    expect(step).toEqual({ setupRequired: true })
    expect(screen.getByTestId('who')).toHaveTextContent('mod@b.test Customer true')
  })
})
