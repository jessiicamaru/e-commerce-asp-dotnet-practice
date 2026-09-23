import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Accounts } from '@/services/accounts'
import type { Account } from '@/services/accounts/types'
import { refusal } from '@/test/refusal'
import { renderAsAdmin, renderAsModerator } from '@/test/render'
import { AdminUsersPage } from '.'

const person = (over: Partial<Account> = {}): Account => ({
  id: 'u-lan', email: 'lan@example.test', firstName: 'Lan', lastName: 'Pham', roles: ['Customer'],
  createdAt: '2026-09-01T00:00:00Z', lockedUntil: null, lockReason: null, bannedAt: null, banReason: null, ...over,
})

const page = (...items: Account[]) => ({ items, page: 1, pageSize: PAGE_SIZE, totalCount: items.length })

function renderPage(as: typeof renderAsAdmin, path = '/admin/users') {
  return as(
    <Routes>
      <Route path="/admin/users" element={<AdminUsersPage />} />
    </Routes>,
    path,
  )
}

async function openActions(user: ReturnType<typeof userEvent.setup>, email: string) {
  await user.click(await screen.findByRole('button', { name: `Actions for ${email}` }))
  return screen.findByRole('menu')
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminUsersPage (specs/043)', () => {
  it('searches by what was typed, from the first page', async () => {
    const search = vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const user = userEvent.setup()
    renderPage(renderAsAdmin, '/admin/users?page=2')
    await screen.findByText('lan@example.test')

    await user.type(screen.getByRole('textbox', { name: 'Email or name' }), 'lan@{Enter}')

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('lan@', 1, PAGE_SIZE))
  })

  it('shows who is locked, until when, and who is banned', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(
      person({ id: 'a', email: 'locked@example.test', lockedUntil: '2026-09-27T10:00:00Z', lockReason: 'Spam' }),
      person({ id: 'b', email: 'banned@example.test', bannedAt: '2026-09-20T00:00:00Z', banReason: 'Fraud' }),
    ))
    renderPage(renderAsAdmin)

    expect(await screen.findByText(/Locked until/)).toBeInTheDocument()
    expect(screen.getByText('Banned')).toBeInTheDocument()
  })

  it('lets an administrator make somebody a moderator', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const grant = vi.spyOn(Accounts, 'grantModerator').mockResolvedValue(person({ roles: ['Customer', 'Moderator'] }))
    const user = userEvent.setup()
    renderPage(renderAsAdmin)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'Make moderator' }))

    await waitFor(() => expect(grant).toHaveBeenCalledWith('u-lan'))
  })

  /** A moderator locks and unlocks; roles and bans are not offered (the server refuses them anyway). */
  it('offers a moderator no roles and no ban', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    const menu = await openActions(user, 'lan@example.test')

    expect(within(menu).getByRole('menuitem', { name: 'Lock…' })).toBeInTheDocument()
    expect(within(menu).queryByRole('menuitem', { name: 'Make moderator' })).not.toBeInTheDocument()
    expect(within(menu).queryByRole('menuitem', { name: 'Ban…' })).not.toBeInTheDocument()
  })

  it('locks for the chosen days with the reason, and offers a moderator nothing past 30', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const lock = vi.spyOn(Accounts, 'lock').mockResolvedValue(person({ lockedUntil: '2026-10-01T00:00:00Z' }))
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'Lock…' }))
    const dialog = await screen.findByRole('dialog')

    expect(within(dialog).queryByRole('button', { name: '90 days' })).not.toBeInTheDocument()
    const confirm = within(dialog).getByRole('button', { name: 'Lock' })
    expect(confirm).toBeDisabled()   // no reason, no lock

    await user.click(within(dialog).getByRole('button', { name: '7 days' }))
    await user.type(within(dialog).getByRole('textbox', { name: 'Reason' }), 'Spam in reviews')
    await user.click(confirm)

    await waitFor(() => expect(lock).toHaveBeenCalledWith('u-lan', 7, 'Spam in reviews'))
  })

  /** Nobody is offered stopping an administrator; a moderator is not offered stopping a moderator. */
  it('does not offer to stop an administrator, or a moderator to a moderator', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(
      person({ id: 'a', email: 'boss@example.test', roles: ['Admin'] }),
      person({ id: 'm', email: 'mod@example.test', roles: ['Customer', 'Moderator'] }),
    ))
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    let menu = await openActions(user, 'boss@example.test')
    expect(within(menu).getByRole('menuitem', { name: 'Lock…' })).toHaveAttribute('aria-disabled', 'true')
    await user.keyboard('{Escape}')

    menu = await openActions(user, 'mod@example.test')
    expect(within(menu).getByRole('menuitem', { name: 'Lock…' })).toHaveAttribute('aria-disabled', 'true')
  })

  it('shows the server refusal in its words', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person({ lockedUntil: '2026-09-27T10:00:00Z' })))
    vi.spyOn(Accounts, 'unlock').mockRejectedValue(refusal(404, 'User not found.'))
    const user = userEvent.setup()
    renderPage(renderAsAdmin)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'Unlock' }))

    expect(await screen.findByText('User not found.')).toBeInTheDocument()
  })
})
