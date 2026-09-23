import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { ShopApplications } from '@/services/shop-applications'
import type { ShopApplication } from '@/services/shop-applications/types'
import { refusal } from '@/test/refusal'
import { renderAsModerator } from '@/test/render'
import { AdminShopsPage } from '.'

const lan: ShopApplication = {
  id: 'a1', userId: 'u1', applicantEmail: 'lan@example.test', applicantName: 'Lan Pham', shopName: 'Lan Film',
  description: 'Film cameras, serviced', phone: '0912 345 678', status: 'Pending', decisionReason: null,
  createdAt: '2026-09-24T08:00:00Z', decidedAt: null,
}
const page = (...items: ShopApplication[]) => ({ items, page: 1, pageSize: PAGE_SIZE, totalCount: items.length })

function renderPage(path = '/admin/shops') {
  return renderAsModerator(
    <Routes>
      <Route path="/admin/shops" element={<AdminShopsPage />} />
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
    renderPage('/admin/shops?status=Rejected')

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
