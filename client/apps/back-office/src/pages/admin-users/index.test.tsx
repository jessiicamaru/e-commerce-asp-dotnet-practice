import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { Accounts } from '@ecommerce/core/services/accounts'
import { Shops } from '@ecommerce/core/services/shops'
import type { Account, ModerationHistoryEntry } from '@ecommerce/core/services/accounts/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsAdmin, renderAsModerator } from '@ecommerce/core/test/render'
import { AdminUsersPage } from '.'

const person = (over: Partial<Account> = {}): Account => ({
  id: 'u-lan', email: 'lan@example.test', firstName: 'Lan', lastName: 'Pham', roles: ['Customer'],
  createdAt: '2026-09-01T00:00:00Z', lockedUntil: null, lockReason: null, bannedAt: null, banReason: null, ...over,
})

const page = (...items: Account[]) => ({ items, page: 1, pageSize: PAGE_SIZE, totalCount: items.length })

function renderPage(as: typeof renderAsAdmin, path = '/users') {
  return as(
    <Routes>
      <Route path="/users" element={<AdminUsersPage />} />
    </Routes>,
    path,
  )
}

async function openActions(user: ReturnType<typeof userEvent.setup>, email: string) {
  await user.click(await screen.findByRole('button', { name: `Actions for ${email}` }))
  return screen.findByRole('menu')
}

const decision = (over: Partial<ModerationHistoryEntry>): ModerationHistoryEntry => ({
  id: 'e1', action: 'AccountLocked', actorEmail: 'mod@example.test', actorRole: 'Moderator', subjectType: 'User',
  subjectId: 'u-lan', summary: 'locked', reason: null, occurredAt: '2026-09-01T10:00:00Z', ...over,
})

const history = (...items: ModerationHistoryEntry[]) => ({ items, page: 1, pageSize: 5, totalCount: items.length })

beforeEach(async () => {
  await i18n.changeLanguage('en')
  // Nobody has a history unless a test says so (specs/100).
  vi.spyOn(Accounts, 'history').mockResolvedValue(history())
})

describe('AdminUsersPage: deleted accounts (specs/123, #241)', () => {
  const gone = person({ id: 'u-gone', email: 'deleted-u-gone@deleted.invalid', firstName: '', lastName: '', roles: [], deletedAt: '2026-09-30T08:00:00Z' })

  it('leaves deleted accounts out until asked, and asks again with them', async () => {
    const search = vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const user = userEvent.setup()
    renderPage(renderAsAdmin)
    await screen.findByText('lan@example.test')
    expect(search).toHaveBeenLastCalledWith('', 1, PAGE_SIZE, false, { role: '', state: '' })

    await user.click(screen.getByRole('checkbox', { name: 'Show deleted accounts' }))

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('', 1, PAGE_SIZE, true, { role: '', state: '' }))
  })

  it('reads Deleted with its date and offers nothing but its history', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(gone))
    const user = userEvent.setup()
    renderPage(renderAsAdmin, '/users?deleted=1')

    const row = (await screen.findByText('deleted-u-gone@deleted.invalid')).closest('tr')!
    expect(within(row).getByText('Deleted account')).toBeInTheDocument()
    expect(within(row).getByText(/^Deleted \d/)).toBeInTheDocument()
    expect(within(row).queryByText('Active')).not.toBeInTheDocument()

    const menu = await openActions(user, gone.email)
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['History…'])
  })
})

describe('AdminUsersPage (specs/043)', () => {
  it('searches by what was typed, from the first page', async () => {
    const search = vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const user = userEvent.setup()
    renderPage(renderAsAdmin, '/users?page=2')
    await screen.findByText('lan@example.test')

    await user.type(screen.getByRole('textbox', { name: 'Email or name' }), 'lan@{Enter}')

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('lan@', 1, PAGE_SIZE, false, { role: '', state: '' }))
  })

  it('shows who is locked, until when, and who is banned', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(
      person({ id: 'a', email: 'locked@example.test', lockedUntil: '2026-09-27T10:00:00Z', lockReason: 'Spam' }),
      person({ id: 'b', email: 'banned@example.test', bannedAt: '2026-09-20T00:00:00Z', banReason: 'Fraud' }),
    ))
    renderPage(renderAsAdmin)

    expect(await screen.findByText(/Locked until/)).toBeInTheDocument()
    expect(within(screen.getByRole('table')).getByText('Banned')).toBeInTheDocument()
  })

  /** specs/095: a ban closes a seller's shop, and the page says so; a customer's ban does not mention a shop. */
  it("says a banned seller's shop is closed", async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(
      person({ id: 's', email: 'seller@example.test', roles: ['Customer', 'Seller'], bannedAt: '2026-09-20T00:00:00Z', banReason: 'Fakes' }),
      person({ id: 'c', email: 'customer@example.test', bannedAt: '2026-09-20T00:00:00Z', banReason: 'Fraud' }),
    ))
    renderPage(renderAsAdmin)

    expect(await screen.findByText('Banned · shop closed')).toBeInTheDocument()
    expect(within(screen.getByRole('table')).getByText('Banned')).toBeInTheDocument()
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

  /** Unlocking obeys what locking does (#121, specs/050) - drawn here, refused by the server on its own. */
  it('does not offer a moderator to unlock a moderator, themselves, or a lock longer than theirs', async () => {
    const soon = new Date(Date.now() + 7 * 86_400_000).toISOString()
    const later = new Date(Date.now() + 200 * 86_400_000).toISOString()
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(
      person({ id: 'm', email: 'mod@example.test', roles: ['Customer', 'Moderator'], lockedUntil: soon }),
      person({ id: 'u1', email: 'me@example.test', roles: ['Customer', 'Moderator'], lockedUntil: soon }),
      person({ id: 'long', email: 'long@example.test', lockedUntil: later }),
      person({ id: 'short', email: 'short@example.test', lockedUntil: soon }),
    ))
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    for (const email of ['mod@example.test', 'me@example.test', 'long@example.test']) {
      const menu = await openActions(user, email)
      expect(within(menu).getByRole('menuitem', { name: 'Unlock' }), email).toHaveAttribute('aria-disabled', 'true')
      await user.keyboard('{Escape}')
    }
    const menu = await openActions(user, 'short@example.test')
    expect(within(menu).getByRole('menuitem', { name: 'Unlock' })).not.toHaveAttribute('aria-disabled')
  })

  it('offers an administrator to unlock anybody but themselves', async () => {
    const later = new Date(Date.now() + 200 * 86_400_000).toISOString()
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(
      person({ id: 'm', email: 'mod@example.test', roles: ['Customer', 'Moderator'], lockedUntil: later }),
    ))
    const user = userEvent.setup()
    renderPage(renderAsAdmin)

    const menu = await openActions(user, 'mod@example.test')
    expect(within(menu).getByRole('menuitem', { name: 'Unlock' })).not.toHaveAttribute('aria-disabled')
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

describe("A person's history (specs/100)", () => {
  /** The issue's acceptance: before locking, the moderator sees the person was locked twice before, and why. */
  it('shows the earlier decisions and their reasons where a lock is decided', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const read = vi.spyOn(Accounts, 'history').mockResolvedValue({
      ...history(
        decision({ id: 'e3', action: 'AccountLocked', reason: 'Abuse in reviews', occurredAt: '2026-09-20T10:00:00Z' }),
        decision({ id: 'e2', action: 'ReviewHidden', reason: 'Advertising', subjectType: 'Review', occurredAt: '2026-09-10T10:00:00Z' }),
        decision({ id: 'e1', action: 'AccountLocked', reason: 'Spam', occurredAt: '2026-09-01T10:00:00Z' }),
      ),
      totalCount: 7,
    })
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'Lock…' }))
    const dialog = await screen.findByRole('dialog')

    const earlier = await within(dialog).findByRole('list', { name: 'Earlier decisions' })
    expect(within(earlier).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      expect.stringMatching(/Account locked.*“Abuse in reviews”/),
      expect.stringMatching(/Review hidden.*“Advertising”/),
      expect.stringMatching(/Account locked.*“Spam”/),
    ])
    expect(within(dialog).getByText('and 4 earlier decisions')).toBeInTheDocument()
    expect(read).toHaveBeenCalledWith('u-lan', 1, 5)
  })

  it('says when there is nothing on record', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'Lock…' }))

    expect(await within(await screen.findByRole('dialog')).findByText(/Nothing on record/)).toBeInTheDocument()
  })

  it("opens the whole history from the person's menu, paged", async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const read = vi.spyOn(Accounts, 'history').mockResolvedValue({
      items: [decision({ action: 'ShopRejected', subjectType: 'ShopApplication', reason: 'Tell us what you sell' })],
      page: 1, pageSize: 10, totalCount: 11,
    })
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'History…' }))
    const dialog = await screen.findByRole('dialog', { name: 'What staff decided about lan@example.test' })

    expect(await within(dialog).findByText('“Tell us what you sell”')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: /next/i }))
    await waitFor(() => expect(read).toHaveBeenLastCalledWith('u-lan', 2, 10))
  })
})

describe('AdminUsersPage, resetting two-factor sign-in (specs/110)', () => {
  it('lets an administrator reset it for somebody who has it on, after saying what that does', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person({ roles: ['Customer', 'Moderator'], twoFactorEnabled: true })))
    const reset = vi.spyOn(Accounts, 'resetTwoFactor').mockResolvedValue()
    const user = userEvent.setup()
    renderPage(renderAsAdmin)

    const menu = await openActions(user, 'lan@example.test')
    await user.click(within(menu).getByRole('menuitem', { name: 'Reset two-factor sign-in' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(dialog).toHaveTextContent('they are emailed at once')
    expect(reset).not.toHaveBeenCalled()

    await user.click(within(dialog).getByRole('button', { name: 'Reset' }))
    await waitFor(() => expect(reset).toHaveBeenCalledWith('u-lan'))
  })

  it('offers nothing to reset when it is off, and nothing to a moderator', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person({ twoFactorEnabled: false }), person({ id: 'u-minh', email: 'minh@example.test', twoFactorEnabled: true })))
    const user = userEvent.setup()
    renderPage(renderAsAdmin)

    const menu = await openActions(user, 'lan@example.test')
    expect(within(menu).queryByRole('menuitem', { name: 'Reset two-factor sign-in' })).not.toBeInTheDocument()
  })

  it('offers a moderator no reset at all', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person({ twoFactorEnabled: true })))
    const user = userEvent.setup()
    renderPage(renderAsModerator)

    const menu = await openActions(user, 'lan@example.test')
    expect(within(menu).queryByRole('menuitem', { name: 'Reset two-factor sign-in' })).not.toBeInTheDocument()
  })
})

describe('AdminUsersPage: by role and state (specs/133, #249)', () => {
  it('asks for the state chosen, and for a role named in the address', async () => {
    const search = vi.spyOn(Accounts, 'search').mockResolvedValue(page(person()))
    const user = userEvent.setup()
    renderPage(renderAsAdmin, '/users?role=Moderator')
    await screen.findByText('lan@example.test')
    expect(search).toHaveBeenLastCalledWith('', 1, PAGE_SIZE, false, { role: 'Moderator', state: '' })

    await user.click(screen.getByRole('tab', { name: 'Locked' }))

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('', 1, PAGE_SIZE, false, { role: 'Moderator', state: 'Locked' }))
  })
})

/** A seller's shop is closed from their row too (specs/137): shops from before applications have no other place. */
describe("AdminUsersPage: closing a seller's shop (specs/137)", () => {
  it('closes the shop of a seller from their row, and offers it for sellers only', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page(person({ roles: ['Seller', 'Customer'] }), person({ id: 'u-an', email: 'an@example.test' })))
    const close = vi.spyOn(Shops, 'close').mockResolvedValue({
      sellerId: 'u-lan', shopName: 'Lan Film', state: 'Closed', pausedAt: null, closedAt: '2026-10-03T08:00:00Z', closedReason: 'Fakes',
    })
    const user = userEvent.setup()
    renderPage(renderAsAdmin)

    await openActions(user, 'an@example.test')
    expect(screen.queryByRole('menuitem', { name: 'Close shop' })).not.toBeInTheDocument()
    await user.keyboard('{Escape}')

    await openActions(user, 'lan@example.test')
    await user.click(screen.getByRole('menuitem', { name: 'Close shop' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Reason (the seller reads this)'), 'Fakes')
    await user.click(within(dialog).getByRole('button', { name: 'Close shop' }))

    await waitFor(() => expect(close).toHaveBeenCalledWith('u-lan', 'Fakes'))
  })
})
