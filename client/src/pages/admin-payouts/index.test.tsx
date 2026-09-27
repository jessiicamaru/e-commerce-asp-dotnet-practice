import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { toast } from 'sonner'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Admin } from '@/services/admin'
import { refusal } from '@/test/refusal'
import { renderAsAdmin } from '@/test/render'
import { AdminPayoutsPage } from '.'

const mai = { sellerId: 's-mai', sellerName: 'Mai Lens Hà Nội', currency: 'VND', due: 4_686_000, parts: 1 }

function renderPage() {
  return renderAsAdmin(
    <Routes>
      <Route path="/admin/payouts" element={<AdminPayoutsPage />} />
    </Routes>,
    '/admin/payouts',
  )
}

const maisAccount = {
  sellerId: 's-mai', bankName: 'Vietcombank', accountHolder: 'NGUYEN THI MAI', accountNumber: '0071001234321',
  updatedAt: '2026-01-01T00:00:00Z',
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  // Every seller on the list has a payout account unless a test says otherwise (specs/106).
  vi.spyOn(Admin, 'payoutAccounts').mockResolvedValue([maisAccount])
})

describe('AdminPayoutsPage', () => {
  it('lists each seller with what is due, in its currency', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([mai])
    renderPage()

    expect(await screen.findByText('Mai Lens Hà Nội')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '₫4,686,000' })).toBeInTheDocument()
  })

  /**
   * A ledger entry that cannot be undone is confirmed first, naming who and how much - and nothing is
   * sent until it is.
   */
  it('confirms, then records the payout and says what was recorded', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([mai])
    const pay = vi.spyOn(Admin, 'pay').mockResolvedValue({
      id: 'p1', sellerId: 's-mai', currency: 'VND', amount: 4_686_000, partCount: 1, createdAt: '',
    })
    const success = vi.spyOn(toast, 'success')
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Record payout/ }))
    expect(await screen.findByText('Record paying Mai Lens Hà Nội?')).toBeInTheDocument()
    expect(screen.getByText(/₫4,686,000 for 1 parcel/)).toBeInTheDocument()
    expect(pay).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Record' }))

    await waitFor(() => expect(pay).toHaveBeenCalledWith('s-mai', 'VND'))
    await waitFor(() => expect(success).toHaveBeenCalledWith('Recorded ₫4,686,000 to Mai Lens Hà Nội.'))
  })

  /** Another administrator got there first: the server's 409, in its words. */
  it('repeats the server when nothing is due any more', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([mai])
    vi.spyOn(Admin, 'pay').mockRejectedValue(refusal(409, 'Nothing is due to this seller in VND.'))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Record payout/ }))
    await user.click(await screen.findByRole('button', { name: 'Record' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Nothing is due to this seller in VND.')
  })

  it('names a seller whose shop name was never recorded by their id', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([{ ...mai, sellerName: null, sellerId: '0199a000-dead-beef' }])
    renderPage()

    expect(await screen.findByText('Seller 0199a000')).toBeInTheDocument()
  })

  it('says so when nothing is due', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('Nothing is due.')).toBeInTheDocument()
  })
})

describe('AdminPayoutsPage payout accounts (specs/106)', () => {
  it('shows where each payout goes, in full, for the administrator paying', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([mai])
    const read = vi.spyOn(Admin, 'payoutAccounts')
    renderPage()

    expect(await screen.findByText('0071001234321')).toBeInTheDocument()
    expect(read).toHaveBeenCalledWith(['s-mai'])
  })

  it('cannot pay a seller with no payout account', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([mai])
    vi.spyOn(Admin, 'payoutAccounts').mockResolvedValue([])
    renderPage()

    expect(await screen.findByText('No payout account - cannot pay')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Record payout|Pay/ })).toBeDisabled()
  })

  it('warns about an account changed in the last week', async () => {
    vi.spyOn(Admin, 'due').mockResolvedValue([mai])
    vi.spyOn(Admin, 'payoutAccounts').mockResolvedValue([{ ...maisAccount, updatedAt: new Date().toISOString() }])
    renderPage()

    expect(await screen.findByText(/check before paying/)).toBeInTheDocument()
  })
})
