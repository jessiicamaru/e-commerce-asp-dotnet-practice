import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Accounts } from '@/services/accounts'
import type { Account } from '@/services/accounts/types'
import { Admin } from '@/services/admin'
import type { StaffOrderSummary } from '@/services/admin/types'
import { Insights } from '@/services/insights'
import { renderAsAdmin } from '@/test/render'
import { AdminOrderSearchPage } from '.'

const order = (over: Partial<StaffOrderSummary> = {}): StaffOrderSummary => ({
  orderId: '01a0dd2b-5f3e-7000-8000-000000000001', userId: 'u-mai', totalAmount: 1_155_000, status: 'Failed',
  failureReason: 'Card declined', itemCount: 1, createdAt: '2026-09-26T08:00:00Z', currency: 'VND', shipmentCount: 1,
  shipmentsShipped: 0, ...over,
})
const mai: Account = {
  id: 'u-mai', email: 'mai@example.com', firstName: 'Mai', lastName: 'Tran', roles: ['Customer'],
  createdAt: '2026-09-01T00:00:00Z', lockedUntil: null, lockReason: null, bannedAt: null, banReason: null,
}
const page = <T,>(...items: T[]) => ({ items, page: 1, pageSize: PAGE_SIZE, totalCount: items.length })

function renderPage(path = '/admin/orders/find') {
  return renderAsAdmin(
    <Routes>
      <Route path="/admin/orders/find" element={<AdminOrderSearchPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Insights, 'people').mockResolvedValue([{ id: 'u-mai', email: 'mai@example.com', firstName: 'Mai', lastName: 'Tran' }])
})

describe('AdminOrderSearchPage (specs/096)', () => {
  it('finds an order by the start of its id, in any status, and says whose it is', async () => {
    const find = vi.spyOn(Admin, 'findOrders').mockResolvedValue(page(order()))
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText('Order id or email'), '01a0dd2b')
    await user.click(screen.getByRole('button', { name: /Find/ }))

    const link = await screen.findByRole('link', { name: /01a0dd2b/ })
    expect(link).toHaveAttribute('href', '/admin/orders/01a0dd2b-5f3e-7000-8000-000000000001')
    expect(await screen.findByText('Mai Tran · mai@example.com')).toBeInTheDocument()
    // The customer's words for the failure (specs/133), not the server's text.
    expect(screen.getByText(/payment was declined/)).toBeInTheDocument()
    expect(screen.queryByText('Card declined')).not.toBeInTheDocument()
    expect(find).toHaveBeenCalledWith({ status: undefined, search: '01a0dd2b', customerId: undefined, page: 1, pageSize: PAGE_SIZE })
  })

  /** Order knows people by id only: an email goes to Identity first, and its id to Order. */
  it("finds a customer's orders by their email", async () => {
    const people = vi.spyOn(Accounts, 'search').mockResolvedValue(page(mai))
    const find = vi.spyOn(Admin, 'findOrders').mockResolvedValue(page(order()))
    renderPage('/admin/orders/find?q=MAI@example.com')

    await waitFor(() => expect(find).toHaveBeenCalledWith(expect.objectContaining({ customerId: 'u-mai', search: undefined })))
    expect(people).toHaveBeenCalledWith('MAI@example.com', 1, 5)
  })

  it('asks Order nothing for an email nobody has', async () => {
    vi.spyOn(Accounts, 'search').mockResolvedValue(page<Account>())
    const find = vi.spyOn(Admin, 'findOrders')
    renderPage('/admin/orders/find?q=nobody@example.com')

    expect(await screen.findByText('Nobody has the address nobody@example.com.')).toBeInTheDocument()
    expect(find).not.toHaveBeenCalled()
  })

  it('narrows to a status from its tab', async () => {
    const find = vi.spyOn(Admin, 'findOrders').mockResolvedValue(page(order()))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('tab', { name: 'Failed' }))

    await waitFor(() => expect(find).toHaveBeenCalledWith(expect.objectContaining({ status: 'Failed', pageSize: PAGE_SIZE })))
  })
})

describe('AdminOrderSearchPage: how many in each state (specs/133, #249)', () => {
  it('says on each tab how many orders the search finds there', async () => {
    const find = vi.spyOn(Admin, 'findOrders').mockImplementation(async (query) =>
      query.pageSize === 1 ? { items: [], page: 1, pageSize: 1, totalCount: query.status === 'Failed' ? 3 : query.status ? 0 : 9 } : page(order()),
    )
    renderPage('/admin/orders/find?q=01a0dd2b')

    expect(await screen.findByRole('tab', { name: /^Failed\s*3$/ })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /^All\s*9$/ })).toBeInTheDocument()
    expect(find).toHaveBeenCalledWith({ status: 'Failed', search: '01a0dd2b', customerId: undefined, page: 1, pageSize: 1 })
  })
})
