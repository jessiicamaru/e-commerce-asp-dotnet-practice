import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { PAGE_SIZE } from '@ecommerce/core/constants/shared'
import { Admin } from '@ecommerce/core/services/admin'
import type { OrderSummary } from '@ecommerce/core/services/order/types'
import { renderAsAdmin } from '@ecommerce/core/test/render'
import { AdminOrdersPage } from '.'

const row: OrderSummary = {
  orderId: 'o-1', totalAmount: 5_709_000, status: 'Paid', failureReason: null, itemCount: 2,
  createdAt: '2026-09-23T08:00:00Z', updatedAt: '2026-09-23T08:00:00Z', currency: 'VND', language: 'vi',
  shipmentCount: 2, shipmentsShipped: 1,
}

function renderPage(path = '/') {
  return renderAsAdmin(
    <Routes>
      <Route path="/" element={<AdminOrdersPage />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('AdminOrdersPage', () => {
  it('opens on the waiting queue and links each order to its page', async () => {
    const queue = vi.spyOn(Admin, 'fulfilment').mockResolvedValue({ items: [row], page: 1, pageSize: PAGE_SIZE, totalCount: 1 })
    renderPage()

    const link = await screen.findByRole('link', { name: /2 items/ })
    expect(link).toHaveAttribute('href', '/orders/o-1')
    // Partly sent, in the row's chip (specs/132).
    expect(screen.getByText('1 of 2 shipped')).toBeInTheDocument()
    expect(queue).toHaveBeenCalledWith('Paid', 1, PAGE_SIZE)
  })

  it('asks for another state when its tab is chosen', async () => {
    const queue = vi.spyOn(Admin, 'fulfilment').mockResolvedValue({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 })
    renderPage()
    await screen.findByText('No orders at this step.')

    await userEvent.click(screen.getByRole('tab', { name: 'Being prepared' }))

    expect(await screen.findByRole('tab', { name: 'Being prepared', selected: true })).toBeInTheDocument()
    expect(queue).toHaveBeenLastCalledWith('Preparing', 1, PAGE_SIZE)
  })

  /** Only the three states the server accepts; anything else in the address falls back to waiting. */
  it('ignores a state it does not know', async () => {
    const queue = vi.spyOn(Admin, 'fulfilment').mockResolvedValue({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 })
    renderPage('/?status=Failed')

    await screen.findByText('No orders at this step.')
    expect(queue).toHaveBeenCalledWith('Paid', 1, PAGE_SIZE)
  })
})
