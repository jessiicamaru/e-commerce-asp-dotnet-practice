import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Audit } from '@/services/audit'
import type { AuditEntry, AuditEntrySummary } from '@/services/audit/types'
import { renderAsAdmin } from '@/test/render'
import { AdminAuditPage } from '.'

const row: AuditEntrySummary = {
  id: 'e-1', category: 'Catalog', action: 'PriceSet', actorId: 'u-1', actorEmail: 'mai@demo.test', actorRole: 'Seller',
  subjectType: 'Variant', subjectId: 'v-1', summary: 'SONY-A7M4 priced at 1200000 VND', service: 'catalog',
  occurredAt: '2026-09-24T08:00:00Z', changeCount: 1,
}
const sweep: AuditEntrySummary = {
  ...row, id: 'e-2', category: 'System', action: 'DeliveriesAutoConfirmed', actorId: null, actorEmail: null, actorRole: null,
  summary: 'Took 2 parcel(s) as delivered', changeCount: 0,
}
const detail: AuditEntry = {
  ...row, recordedAt: '2026-09-24T08:00:01Z', before: { price: 1000000 }, after: { price: 1200000 },
  changes: [{ path: 'price', before: 1000000, after: 1200000 }],
}

function renderPage(path = '/admin/audit') {
  return renderAsAdmin(
    <Routes>
      <Route path="/admin/audit" element={<AdminAuditPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Audit, 'summary').mockResolvedValue([{ category: 'Catalog', count: 3 }, { category: 'System', count: 1 }])
})

describe('AdminAuditPage', () => {
  it('lists entries with who did them, and the system when nobody did', async () => {
    vi.spyOn(Audit, 'list').mockResolvedValue({ items: [row, sweep], page: 1, pageSize: PAGE_SIZE, totalCount: 2 })
    renderPage()

    expect(await screen.findByText('Price set')).toBeInTheDocument()
    expect(screen.getByText('mai@demo.test')).toBeInTheDocument()
    expect(screen.getByText('System', { selector: 'td' })).toBeInTheDocument()
    // The tab counts, and the total on "All".
    expect(await screen.findByRole('tab', { name: /Catalog\s*3/ })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /All\s*4/ })).toBeInTheDocument()
  })

  it('asks for one category when its tab is chosen', async () => {
    const list = vi.spyOn(Audit, 'list').mockResolvedValue({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 })
    renderPage()
    await screen.findByText('Nothing matches.')

    await userEvent.click(screen.getByRole('tab', { name: /Payments/ }))

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ category: 'Payment' }), 1, PAGE_SIZE))
  })

  it('filters by who did it', async () => {
    const list = vi.spyOn(Audit, 'list').mockResolvedValue({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 })
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('Nothing matches.')

    await user.type(screen.getByLabelText(/Filter by who did it/), 'mai@demo.test{Enter}')

    await waitFor(() => expect(list).toHaveBeenLastCalledWith(expect.objectContaining({ actor: 'mai@demo.test' }), 1, PAGE_SIZE))
  })

  /** The whole point of the log: what it was, what it became. */
  it('opens an entry to what changed, field by field', async () => {
    vi.spyOn(Audit, 'list').mockResolvedValue({ items: [row], page: 1, pageSize: PAGE_SIZE, totalCount: 1 })
    const get = vi.spyOn(Audit, 'get').mockResolvedValue(detail)
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Price set' }))

    const changes = await screen.findByRole('table', { name: 'Changed fields' })
    expect(within(changes).getByText('1000000')).toBeInTheDocument()
    expect(within(changes).getByText('1200000')).toBeInTheDocument()
    expect(get).toHaveBeenCalledWith('e-1')
  })
})
