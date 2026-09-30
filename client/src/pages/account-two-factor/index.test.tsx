import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '@/context/auth/useAuth'
import type { AuthState } from '@/context/auth/types'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Auth } from '@/services/auth'
import type { TwoFactorStatus } from '@/services/auth/types'
import { refusal } from '@/test/refusal'
import { renderAsCustomer, renderAsModerator } from '@/test/render'
import { AccountTwoFactorPage } from '.'

const off: TwoFactorStatus = { enabled: false, enabledAt: null, recoveryCodesLeft: 0, required: false }
const codes = Array.from({ length: 10 }, (_, i) => `AAAA${i}-BBBB${i}`)

/** Signed in as a moderator whose session must be renewed once setup is confirmed - the renewal is what brings the role. */
function renderWithSession(refreshSession: () => Promise<boolean>) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const value = {
    user: { id: 'u1', email: 'mod@b.test', firstName: 'Mai', lastName: 'T', roles: ['Customer'], emailConfirmed: true, twoFactorSetupRequired: true },
    restoring: false, isSeller: false, isAdmin: false, isStaff: false, refreshSession,
    signIn: async () => ({ setupRequired: true }), completeSignIn: async () => {}, signUp: async () => {}, signOut: async () => {},
  } as AuthState
  return render(
    <AuthContext.Provider value={value}>
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <AccountTwoFactorPage />
        </MemoryRouter>
      </QueryClientProvider>
    </AuthContext.Provider>,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AccountTwoFactorPage (specs/110)', () => {
  it('sets it up: the key to type, a code to confirm, then the recovery codes once and a renewed session', async () => {
    vi.spyOn(Auth, 'twoFactor').mockResolvedValue(off)
    vi.spyOn(Auth, 'setUpTwoFactor').mockResolvedValue({ secret: 'JBSWY3DPEHPK3PXP', uri: 'otpauth://totp/Shop:mod?secret=JBSWY3DPEHPK3PXP' })
    const confirm = vi.spyOn(Auth, 'confirmTwoFactor').mockResolvedValue(codes)
    const refreshed = vi.fn(async () => true)
    const user = userEvent.setup()
    renderWithSession(refreshed)

    await user.click(await screen.findByRole('button', { name: 'Set it up' }))
    expect(await screen.findByTestId('two-factor-secret')).toHaveTextContent('JBSW Y3DP EHPK 3PXP')

    const confirmButton = screen.getByRole('button', { name: 'Turn on' })
    expect(confirmButton).toBeDisabled()
    await user.type(screen.getByLabelText('Code from the app'), '123 456')
    await user.click(confirmButton)

    await waitFor(() => expect(confirm).toHaveBeenCalledWith('123456'))
    const list = await screen.findByRole('list', { name: 'Your recovery codes' })
    expect(within(list).getAllByRole('listitem')).toHaveLength(10)
    expect(refreshed).toHaveBeenCalled()
  })

  it('shows a wrong code in its own words and stays on the step', async () => {
    vi.spyOn(Auth, 'twoFactor').mockResolvedValue(off)
    vi.spyOn(Auth, 'setUpTwoFactor').mockResolvedValue({ secret: 'JBSWY3DPEHPK3PXP', uri: 'otpauth://totp/x' })
    vi.spyOn(Auth, 'confirmTwoFactor').mockRejectedValue(refusal(400, undefined, { errors: { Code: ['The code is not right, or was already used.'] } }))
    const user = userEvent.setup()
    renderAsModerator(<AccountTwoFactorPage />)

    await user.click(await screen.findByRole('button', { name: 'Set it up' }))
    await user.type(await screen.findByLabelText('Code from the app'), '000000')
    await user.click(screen.getByRole('button', { name: 'Turn on' }))

    expect(await screen.findByText(/That code is not right, or was already used/)).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Your recovery codes' })).not.toBeInTheDocument()
  })

  /** Staff keep it on: no way to turn it off is drawn - and the server refuses anyway. */
  it('offers staff no way to turn it off', async () => {
    vi.spyOn(Auth, 'twoFactor').mockResolvedValue({ enabled: true, enabledAt: '2026-09-30T08:00:00Z', recoveryCodesLeft: 7, required: true })
    renderAsModerator(<AccountTwoFactorPage />)

    expect(await screen.findByText(/7 recovery codes left/)).toBeInTheDocument()
    expect(screen.getByText(/Staff accounts keep two-factor sign-in on/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Turn off' })).not.toBeInTheDocument()
  })

  it('lets a customer turn it off with the password and a code', async () => {
    vi.spyOn(Auth, 'twoFactor').mockResolvedValue({ enabled: true, enabledAt: '2026-09-30T08:00:00Z', recoveryCodesLeft: 10, required: false })
    const turnOff = vi.spyOn(Auth, 'turnOffTwoFactor').mockResolvedValue()
    const user = userEvent.setup()
    renderAsCustomer(<AccountTwoFactorPage />)

    await user.type(await screen.findByLabelText('Password'), 'Right-Passw0rd')
    await user.type(screen.getByLabelText('Code'), '654 321')
    await user.click(screen.getByRole('button', { name: 'Turn off' }))

    await waitFor(() => expect(turnOff).toHaveBeenCalledWith('Right-Passw0rd', '654321'))
  })

  it('makes new recovery codes with a code and shows them', async () => {
    vi.spyOn(Auth, 'twoFactor').mockResolvedValue({ enabled: true, enabledAt: '2026-09-30T08:00:00Z', recoveryCodesLeft: 2, required: true })
    const renew = vi.spyOn(Auth, 'newRecoveryCodes').mockResolvedValue(codes)
    const user = userEvent.setup()
    renderAsModerator(<AccountTwoFactorPage />)

    await user.type(await screen.findByLabelText('New recovery codes'), '111222')
    await user.click(screen.getByRole('button', { name: 'Make new codes' }))

    await waitFor(() => expect(renew).toHaveBeenCalledWith('111222'))
    expect(await screen.findByRole('list', { name: 'Your recovery codes' })).toBeInTheDocument()
  })
})
