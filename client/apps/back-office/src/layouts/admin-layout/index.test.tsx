import { screen, waitFor, within } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { AdminHome } from '@/pages/admin-home'
import { Accounts } from '@ecommerce/core/services/accounts'
import { Admin } from '@ecommerce/core/services/admin'
import { Moderation } from '@ecommerce/core/services/moderation'
import { Reports } from '@ecommerce/core/services/reports'
import { ShopApplications } from '@ecommerce/core/services/shop-applications'
import { renderAsAdmin, renderAsModerator } from '@ecommerce/core/test/render'
import { AdminLayout } from '.'

function renderConsole(as: typeof renderAsAdmin, path: string | { pathname: string; state: unknown } = '/') {
  return as(
    <Routes>
      <Route path="/" element={<AdminLayout />}>
        <Route index element={<AdminHome />} />
        <Route path="orders/:id" element={<p>an order</p>} />
        <Route path="users" element={<p>the users page</p>} />
        <Route path="shops" element={<p>the shops page</p>} />
        <Route path="moderation" element={<p>the moderation page</p>} />
      </Route>
    </Routes>,
    path as string,
  )
}

const page = (totalCount: number) => ({ items: [], page: 1, pageNumber: 1, pageSize: 1, totalCount, totalPages: 0, hasPreviousPage: false, hasNextPage: false }) as never

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Accounts, 'search').mockResolvedValue({ items: [], page: 1, pageSize: 12, totalCount: 0 })
  vi.spyOn(Admin, 'fulfilment').mockResolvedValue({ items: [], page: 1, pageSize: 12, totalCount: 0 })
})

describe('AdminLayout (specs/043)', () => {
  it('shows an administrator every page', async () => {
    renderConsole(renderAsAdmin)

    for (const name of ['Overview', 'Orders to ship', 'Returns', 'Seller payouts', 'Vouchers', 'Moderation', 'Products to review', 'Shop applications', 'Users', 'Audit log']) {
      expect(await screen.findByRole('link', { name: new RegExp(name) })).toBeInTheDocument()
    }
  })

  /** A link to a page that answers 403 is a link to an error. */
  it('shows a moderator only the pages a moderator can use, and opens on their dashboard', async () => {
    renderConsole(renderAsModerator)

    expect(await screen.findByText('the moderation page')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Shop applications/ })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Users/ })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Seller payouts/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Returns/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Vouchers/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Audit log/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Overview/ })).not.toBeInTheDocument()
  })
})

describe('AdminLayout groups and counts (specs/129, #246)', () => {
  beforeEach(() => {
    vi.spyOn(Admin, 'fulfilment').mockImplementation(async (_status, _page, pageSize) => page(pageSize === 1 ? 3 : 0))
    vi.spyOn(Admin, 'returns').mockResolvedValue(page(1))
    vi.spyOn(Moderation, 'products').mockResolvedValue(page(14))
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(2))
    vi.spyOn(Reports, 'queue').mockResolvedValue(page(0))
  })

  it('groups the links and shows what is waiting, nothing for an empty queue', async () => {
    renderConsole(renderAsAdmin)

    for (const heading of ['Orders', 'Money', 'Catalogue', 'Moderation', 'Messages', 'System']) {
      expect(screen.getByRole('group', { name: heading })).toBeInTheDocument()
    }
    const orders = screen.getByRole('group', { name: 'Orders' })
    expect(await within(orders).findByLabelText('3 waiting')).toBeInTheDocument()
    expect(within(orders).getByLabelText('1 waiting')).toBeInTheDocument()
    const moderation = screen.getByRole('group', { name: 'Moderation' })
    expect(await within(moderation).findByLabelText('14 waiting')).toBeInTheDocument()
    expect(within(moderation).getByLabelText('2 waiting')).toBeInTheDocument()
    expect(within(screen.getByRole('link', { name: /Reports/ })).queryByLabelText(/waiting/)).not.toBeInTheDocument()
  })

  it("asks a moderator only for their own queues' counts", async () => {
    renderConsole(renderAsModerator)

    expect(await screen.findByLabelText('14 waiting')).toBeInTheDocument()
    expect(screen.queryByRole('group', { name: 'Money' })).not.toBeInTheDocument()
    await waitFor(() => expect(Moderation.products).toHaveBeenCalled())
    expect(Admin.returns).not.toHaveBeenCalled()
    expect(vi.mocked(Admin.fulfilment).mock.calls.filter(([, , size]) => size === 1)).toHaveLength(0)
  })

  it('keeps the list an order was opened from lit', async () => {
    renderConsole(renderAsAdmin, { pathname: '/orders/o1', state: { from: '/orders/find?q=01a0' } })

    await screen.findByText('an order')
    expect(screen.getByRole('link', { name: /Find an order/ }).className).toContain('bg-primary')
    expect(screen.getByRole('link', { name: /Orders to ship/ }).className).not.toContain('bg-primary')
  })

  it('falls back to Orders to ship for an order opened directly', async () => {
    renderConsole(renderAsAdmin, '/orders/o1')

    await screen.findByText('an order')
    expect(screen.getByRole('link', { name: /Orders to ship/ }).className).toContain('bg-primary')
  })
})
