import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { PAGE_SIZE } from '@/constants/shared'
import { Order } from '@/services/order'
import { Seller } from '@/services/seller'
import { renderAsSeller } from '@/test/render'
import { ShopPayoutsPage } from '.'

function renderPage(path = '/shop/payouts') {
  return renderAsSeller(
    <Routes>
      <Route path="/shop/payouts" element={<ShopPayoutsPage />} />
    </Routes>,
    path,
  )
}

const empty = { items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 }

beforeEach(async () => {
  // Pinned: the page would otherwise assert English on one machine and Vietnamese on another.
  await i18n.changeLanguage('en')
  // No payout account unless a test gives one (specs/106).
  vi.spyOn(Seller, 'payoutAccount').mockResolvedValue(null)
})

describe('ShopPayoutsPage', () => {
  /** The shop converts nothing (specs/022): dong and dollars are two balances, never one sum. */
  it('shows one balance per currency, with where the money is', async () => {
    vi.spyOn(Order, 'balance').mockResolvedValue([
      { currency: 'USD', onTheWay: 0, due: 10.68, paidOut: 0 },
      { currency: 'VND', onTheWay: 1_095_000, due: 0, paidOut: 2_300_000 },
    ])
    vi.spyOn(Order, 'payouts').mockResolvedValue(empty)
    renderPage()

    // A generous wait: under the whole suite's parallel load the first render has taken over a second.
    const dong = await screen.findByRole('generic', { name: 'Balance in VND' }, { timeout: 5000 })
    expect(within(dong).getByText('On the way').nextElementSibling).toHaveTextContent('₫1,095,000')
    expect(within(dong).getByText('Paid out').nextElementSibling).toHaveTextContent('₫2,300,000')

    const dollars = screen.getByRole('generic', { name: 'Balance in USD' })
    expect(within(dollars).getByText('Due').nextElementSibling).toHaveTextContent('$10.68')
  })

  it('lists the payouts made, a page at a time, and asks for its own only', async () => {
    vi.spyOn(Order, 'balance').mockResolvedValue([{ currency: 'VND', onTheWay: 0, due: 0, paidOut: 1_095_000 }])
    const payouts = vi.spyOn(Order, 'payouts').mockResolvedValue({
      items: [{ id: 'p1', sellerId: 's1', currency: 'VND', amount: 1_095_000, partCount: 2, createdAt: '2026-09-23T10:00:00Z' }],
      page: 2, pageSize: PAGE_SIZE, totalCount: 13,
    })
    renderPage('/shop/payouts?page=2')

    expect(await screen.findByText('2 sales')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '₫1,095,000' })).toBeInTheDocument()
    // The page and the one page size; no seller id - the token says whose.
    expect(payouts).toHaveBeenCalledWith(2, PAGE_SIZE)
  })

  it('says so when there is nothing yet', async () => {
    vi.spyOn(Order, 'balance').mockResolvedValue([])
    vi.spyOn(Order, 'payouts').mockResolvedValue(empty)
    renderPage()

    expect(await screen.findByText(/Nothing yet/)).toBeInTheDocument()
    expect(screen.getByText('The shop has not paid you out yet.')).toBeInTheDocument()
  })

  it('says so when it cannot load', async () => {
    vi.spyOn(Order, 'balance').mockRejectedValue(new Error('down'))
    vi.spyOn(Order, 'payouts').mockResolvedValue(empty)
    renderPage()

    expect(await screen.findByText('Could not load your payouts.')).toBeInTheDocument()
  })
})

describe('ShopPayoutsPage payout account (specs/106)', () => {
  const quiet = () => {
    vi.spyOn(Order, 'balance').mockResolvedValue([])
    vi.spyOn(Order, 'payouts').mockResolvedValue({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0 })
  }

  it('shows the account masked, as the server sends it', async () => {
    quiet()
    vi.spyOn(Seller, 'payoutAccount').mockResolvedValue({
      bankName: 'Vietcombank', accountHolder: 'NGUYEN THI MAI', accountNumberMasked: '•••• 4321', updatedAt: '2026-09-27T10:00:00Z',
    })
    renderPage()

    expect(await screen.findByText('•••• 4321')).toBeInTheDocument()
    expect(screen.getByText(/Vietcombank · NGUYEN THI MAI/)).toBeInTheDocument()
  })

  it('saves what was typed, and says a change is emailed', async () => {
    quiet()
    const save = vi.spyOn(Seller, 'setPayoutAccount').mockResolvedValue({
      bankName: 'Vietcombank', accountHolder: 'NGUYEN THI MAI', accountNumberMasked: '•••• 4321', updatedAt: '2026-09-27T10:00:00Z',
    })
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText(/We email you whenever this changes/)).toBeInTheDocument()
    await user.type(screen.getByLabelText('Bank'), ' Vietcombank ')
    await user.type(screen.getByLabelText('Account holder'), 'NGUYEN THI MAI')
    await user.type(screen.getByLabelText('Account number'), '0071 0012 34321')
    await user.click(screen.getByRole('button', { name: 'Save account' }))

    await waitFor(() => expect(save).toHaveBeenCalledWith({
      bankName: 'Vietcombank', accountHolder: 'NGUYEN THI MAI', accountNumber: '0071 0012 34321',
    }))
  })
})
