import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { UserMenu } from '.'

const user = { id: 'u1', email: 'mai@demo.test', firstName: 'Mai', lastName: 'Trần', roles: [] as string[] }

/**
 * Opens the menu and waits for it: base-ui opens asynchronously, so asserting synchronously right after
 * the click passes or fails depending on timing - it did both before this waited.
 */
async function open(isSeller: boolean, onSignOut = vi.fn(), isStaff = false) {
  const clicker = userEvent.setup()
  render(
    <MemoryRouter>
      <UserMenu user={user} isSeller={isSeller} isStaff={isStaff} onSignOut={onSignOut} />
    </MemoryRouter>,
  )
  await clicker.click(screen.getByRole('button', { name: 'Account' }))
  await screen.findByRole('menu')
  return clicker
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('UserMenu', () => {
  it('shows who is signed in', async () => {
    await open(false)

    expect(screen.getByText('mai@demo.test')).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /Orders/ })).toBeInTheDocument()
  })

  /** specs/038: the console is drawn for an administrator only - again courtesy, not the permission. */
  it('offers the console to an administrator and nobody else', async () => {
    await open(false, vi.fn(), true)
    expect(screen.getByRole('menuitem', { name: /Admin/ })).toBeInTheDocument()
  })

  it('does not offer the console to a customer', async () => {
    await open(true)
    expect(screen.queryByRole('menuitem', { name: /Admin/ })).not.toBeInTheDocument()
  })

  /** Drawn for a seller only - and that is courtesy: the endpoints refuse anybody else on their own. */
  it('does not offer a shop to a customer', async () => {
    await open(false)

    expect(screen.queryByRole('menuitem', { name: /My shop/ })).not.toBeInTheDocument()
  })

  it('offers the shop to a seller', async () => {
    await open(true)

    expect(screen.getByRole('menuitem', { name: /My shop/ })).toBeInTheDocument()
  })

  it('signs out', async () => {
    const onSignOut = vi.fn()
    const clicker = await open(false, onSignOut)

    await clicker.click(screen.getByRole('menuitem', { name: /Sign out/ }))

    expect(onSignOut).toHaveBeenCalled()
  })
})
