import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Address } from '@/services/address'
import type { Address as AddressModel } from '@/services/address/types'
import { Order } from '@/services/order'
import { renderAsSeller } from '@/test/render'
import { CheckoutPage } from '.'

const saved: AddressModel = {
  id: 'a-new', recipientName: 'Lan Pham', line1: '12 Ly Thuong Kiet', line2: null, city: 'Ha Noi', region: null,
  postalCode: '100000', country: 'VN', phone: null, isDefault: true,
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Order, 'shippingOptions').mockResolvedValue([{ code: 'standard', name: 'Standard', price: 30000, currency: 'VND' }])
  vi.spyOn(Order, 'quote').mockRejectedValue(new Error('not under test'))
})

describe('CheckoutPage without an address', () => {
  /**
   * The page used to say "you need an address" and link away to another page that did not bring the
   * customer back. The address is added here, in a dialog, and checkout carries on with it.
   */
  it('adds an address in place and prices the order to it', async () => {
    vi.spyOn(Address, 'list').mockResolvedValueOnce([]).mockResolvedValue([saved])
    const create = vi.spyOn(Address, 'create').mockResolvedValue(saved)
    const user = userEvent.setup()
    renderAsSeller(<CheckoutPage />, '/checkout')

    await user.click(await screen.findByRole('button', { name: /Add one/i }))
    const dialog = await screen.findByRole('dialog')
    expect(dialog).toHaveTextContent(/New address/i)

    fireEvent.change(screen.getByLabelText(/Recipient name/i), { target: { value: 'Lan Pham' } })
    fireEvent.change(screen.getByLabelText(/Address line 1/i), { target: { value: '12 Ly Thuong Kiet' } })
    fireEvent.change(screen.getByLabelText(/^City/i), { target: { value: 'Ha Noi' } })
    fireEvent.change(screen.getByLabelText(/Postal code/i), { target: { value: '100000' } })
    await user.click(screen.getByRole('button', { name: /^Save$/i }))

    await waitFor(() => expect(create).toHaveBeenCalled())
    // The country defaults to Vietnam and is sent as the two-letter code the tax rate is looked up by.
    expect(create.mock.calls[0][0]).toMatchObject({ recipientName: 'Lan Pham', country: 'VN' })
    await waitFor(() => expect(Order.quote).toHaveBeenCalledWith({ addressId: 'a-new', shippingOption: 'standard' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
