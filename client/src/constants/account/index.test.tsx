import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { TopBar } from '@/components/layout/top-bar'
import { UserMenu } from '@/components/layout/user-menu'
import i18n from '@/config/i18n'
import { AccountPage } from '@/pages/account'
import { Auth } from '@/services/auth'
import type { User } from '@/services/auth/types'
import { Cart } from '@/services/cart'
import { Notifications } from '@/services/notifications'
import { renderAsAdmin, renderAsCustomer, renderAsSeller } from '@/test/render'
import { accountDestinations } from '.'

const user = { id: 'u1', email: 'a@b.test', firstName: 'Mai', lastName: 'T', roles: [] } as unknown as User

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Cart, 'get').mockResolvedValue({ lines: [] } as never)
  vi.spyOn(Notifications, 'unreadCount').mockResolvedValue(0 as never)
  vi.spyOn(Auth, 'me').mockResolvedValue({ firstName: 'Mai', lastName: 'T', phone: null, email: 'a@b.test' } as never)
})

const people = [
  { who: 'a customer', render: renderAsCustomer, roles: { isSeller: false, isStaff: false } },
  { who: 'a seller', render: renderAsSeller, roles: { isSeller: true, isStaff: false } },
  { who: 'an administrator', render: renderAsAdmin, roles: { isSeller: false, isStaff: true } },
]

/** The words of the one list, in order (specs/127, #254). */
const expected = (roles: { isSeller: boolean; isStaff: boolean }) => accountDestinations(roles).map(({ label }) => i18n.t(label))

describe('one account menu everywhere (specs/127, #254)', () => {
  it.each(people)('the avatar menu offers the list to $who', async ({ render, roles }) => {
    render(<UserMenu user={user} isSeller={roles.isSeller} isStaff={roles.isStaff} onSignOut={() => {}} />)

    await userEvent.setup().click(screen.getByRole('button', { name: 'Account' }))

    const items = within(await screen.findByRole('menu')).getAllByRole('menuitem').map((item) => item.textContent?.trim())
    expect(items.slice(0, -1)).toEqual(expected(roles)) // the last one is Sign out
  })

  it.each(people)('the phone menu offers the list to $who', async ({ render, roles }) => {
    render(<TopBar />)

    await userEvent.setup().click(screen.getByRole('button', { name: 'Menu' }))

    const sheet = await screen.findByRole('dialog')
    const links = within(sheet).getAllByRole('link').map((link) => link.textContent?.trim())
    expect(links).toEqual(expected(roles))
  })

  it.each(people)('the account page offers the list to $who, less itself', async ({ render, roles }) => {
    render(<AccountPage />)

    const nav = screen.getByRole('navigation', { name: 'Your account' })
    const links = within(nav).getAllByRole('link').map((link) => link.textContent?.trim())
    expect(links).toEqual(expected(roles).slice(1))
  })

  it('offers Saved, Notifications and two-factor sign-in - what the menus were missing', () => {
    const labels = expected({ isSeller: false, isStaff: false })
    expect(labels).toEqual(expect.arrayContaining(['Saved', 'Notifications', 'Two-factor sign-in', 'Open a shop']))
  })
})
