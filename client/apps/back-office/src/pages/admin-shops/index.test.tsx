import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { ShopApplications } from '@ecommerce/core/services/shop-applications'
import { Shops } from '@ecommerce/core/services/shops'
import type { ShopApplication } from '@ecommerce/core/services/shop-applications/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsModerator } from '@ecommerce/core/test/render'
import { AdminShopsPage } from '.'

const lan: ShopApplication = {
  id: 'a1', userId: 'u1', applicantEmail: 'lan@example.test', applicantName: 'Lan Pham', shopName: 'Lan Film',
  description: 'Film cameras, serviced', phone: '0912 345 678', status: 'Pending', decisionReason: null,
  createdAt: '2026-09-24T08:00:00Z', decidedAt: null,
}
const page = (...items: ShopApplication[]) => ({ items, page: 1, pageSize: PAGE_SIZE, totalCount: items.length })

function renderPage(path = '/shops') {
  return renderAsModerator(
    <Routes>
      <Route path="/shops" element={<AdminShopsPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminShopsPage (specs/044)', () => {
  it('opens on the waiting ones, with who applied and what they sell', async () => {
    const list = vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(lan))
    renderPage()

    expect(await screen.findByText('Lan Film')).toBeInTheDocument()
    expect(screen.getByText(/lan@example.test/)).toBeInTheDocument()
    expect(screen.getByText('Film cameras, serviced')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith('Pending', 1, PAGE_SIZE)
  })

  it('approves at a press', async () => {
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(lan))
    const approve = vi.spyOn(ShopApplications, 'approve').mockResolvedValue({ ...lan, status: 'Approved' })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Approve' }))

    await waitFor(() => expect(approve).toHaveBeenCalledWith('a1'))
  })

  /** The applicant reads the reason, so there is no rejecting without one. */
  it('rejects only with a reason', async () => {
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(lan))
    const reject = vi.spyOn(ShopApplications, 'reject').mockResolvedValue({ ...lan, status: 'Rejected' })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Reject…' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Reject' })
    expect(confirm).toBeDisabled()

    await user.type(within(dialog).getByLabelText('Reason'), 'Tell us more')
    await user.click(confirm)

    await waitFor(() => expect(reject).toHaveBeenCalledWith('a1', 'Tell us more'))
  })

  it('shows the decided ones on their own tab, without buttons', async () => {
    const list = vi.spyOn(ShopApplications, 'list').mockResolvedValue(page({ ...lan, status: 'Rejected', decisionReason: 'Too vague' }))
    renderPage('/shops?status=Rejected')

    expect(await screen.findByText('Too vague')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument()
    expect(list).toHaveBeenCalledWith('Rejected', 1, PAGE_SIZE)
  })

  it('shows somebody else deciding first as the server says it', async () => {
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(lan))
    vi.spyOn(ShopApplications, 'approve').mockRejectedValue(refusal(409, 'This application is already approved.'))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Approve' }))

    expect(await screen.findByText('This application is already approved.')).toBeInTheDocument()
  })
})

describe('AdminShopsPage and unconfirmed applicants (specs/063)', () => {
  it('marks an applicant whose address is not confirmed - approval waits for it', async () => {
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(
      { ...lan, applicantEmailConfirmed: false },
      { ...lan, id: 'a2', shopName: 'Minh Lens', applicantEmailConfirmed: true },
    ))
    renderPage()

    expect(await screen.findByText('Lan Film')).toBeInTheDocument()
    expect(screen.getAllByText(i18n.t('admin:shops.unconfirmed'))).toHaveLength(1)
  })
})

describe('AdminShopsPage, the closed shops (specs/107)', () => {
  const closed = {
    sellerId: 's1', shopName: 'Mai Lens', state: 'Closed' as const, pausedAt: '2026-09-26T08:00:00Z',
    closedAt: '2026-09-27T08:00:00Z', closedReason: 'Counterfeit listings',
  }
  const closedPage = { items: [closed], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false }

  /** A different list behind the same tabs: it must not ask for applications with a status that is not one. */
  it('lists the closed shops with the reason, and reopens one', async () => {
    const applications = vi.spyOn(ShopApplications, 'list')
    const list = vi.spyOn(Shops, 'closed').mockResolvedValue(closedPage)
    const reopen = vi.spyOn(Shops, 'reopen').mockResolvedValue({ ...closed, state: 'Paused', closedAt: null, closedReason: null })
    const user = userEvent.setup()
    renderPage('/shops?status=Closed')

    expect(await screen.findByText('Counterfeit listings')).toBeInTheDocument()
    expect(screen.getByText('Also paused by its seller')).toBeInTheDocument()
    expect(list).toHaveBeenCalledWith(1, PAGE_SIZE)
    expect(applications).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Reopen' }))
    await waitFor(() => expect(reopen).toHaveBeenCalledWith('s1'))
  })
})

/** Closing a shop moved here from its public page (specs/137). */
describe('AdminShopsPage: closing a shop (specs/137)', () => {
  it('closes an approved shop with a reason', async () => {
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page({ ...lan, status: 'Approved', decidedAt: '2026-09-25T08:00:00Z' }))
    const close = vi.spyOn(Shops, 'close').mockResolvedValue({
      sellerId: 'u1', shopName: 'Lan Film', state: 'Closed', pausedAt: null, closedAt: '2026-10-03T08:00:00Z', closedReason: 'Fakes',
    })
    vi.spyOn(Shops, 'closed').mockResolvedValue({ items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false })
    const user = userEvent.setup()
    renderPage('/shops?status=Approved')

    await user.click(await screen.findByRole('button', { name: 'Close shop' }))
    const dialog = await screen.findByRole('dialog')
    const confirm = within(dialog).getByRole('button', { name: 'Close shop' })
    expect(confirm).toBeDisabled()
    await user.type(within(dialog).getByLabelText('Reason (the seller reads this)'), '  Fakes ')
    await user.click(confirm)

    await waitFor(() => expect(close).toHaveBeenCalledWith('u1', 'Fakes'))
  })

  it('offers no close on an application still waiting', async () => {
    vi.spyOn(ShopApplications, 'list').mockResolvedValue(page(lan))
    renderPage()

    expect(await screen.findByText('Lan Film')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Close shop' })).not.toBeInTheDocument()
  })
})
