import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { AdminHome } from '@/pages/admin-home'
import { Accounts } from '@/services/accounts'
import { Admin } from '@/services/admin'
import { renderAsAdmin, renderAsModerator } from '@/test/render'
import { AdminLayout } from '.'

function renderConsole(as: typeof renderAsAdmin) {
  return as(
    <Routes>
      <Route path="/admin" element={<AdminLayout />}>
        <Route index element={<AdminHome />} />
        <Route path="users" element={<p>the users page</p>} />
        <Route path="shops" element={<p>the shops page</p>} />
        <Route path="moderation" element={<p>the moderation page</p>} />
      </Route>
    </Routes>,
    '/admin',
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Accounts, 'search').mockResolvedValue({ items: [], page: 1, pageSize: 12, totalCount: 0 })
  vi.spyOn(Admin, 'fulfilment').mockResolvedValue({ items: [], page: 1, pageSize: 12, totalCount: 0 })
})

describe('AdminLayout (specs/043)', () => {
  it('shows an administrator every page', async () => {
    renderConsole(renderAsAdmin)

    for (const name of ['Orders to ship', 'Seller payouts', 'Moderation', 'Products to review', 'Shop applications', 'Users', 'Audit log']) {
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
    expect(screen.queryByRole('link', { name: /Audit log/ })).not.toBeInTheDocument()
  })
})
