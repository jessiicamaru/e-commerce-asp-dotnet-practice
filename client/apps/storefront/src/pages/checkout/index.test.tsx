import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Address } from '@ecommerce/core/services/address'
import type { Address as AddressModel } from '@ecommerce/core/services/address/types'
import { Order } from '@ecommerce/core/services/order'
import { Payment } from '@ecommerce/core/services/payment'
import { Voucher } from '@ecommerce/core/services/voucher'
import type { Order as OrderModel, Quote } from '@ecommerce/core/services/order/types'
import { refusal } from '@ecommerce/core/test/refusal'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { CheckoutPage } from '.'

const saved: AddressModel = {
  id: 'a-new', recipientName: 'Lan Pham', line1: '12 Ly Thuong Kiet', line2: null, city: 'Ha Noi', region: null,
  postalCode: '100000', country: 'VN', phone: null, isDefault: true,
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Order, 'shippingOptions').mockResolvedValue([{ code: 'standard', name: 'Standard', price: 30000, currency: 'VND' }])
  vi.spyOn(Order, 'quote').mockRejectedValue(new Error('not under test'))
  vi.spyOn(Payment, 'about').mockResolvedValue({ movesMoney: null, atGateway: false })
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
    await waitFor(() => expect(Order.quote).toHaveBeenCalledWith({ addressId: 'a-new', shippingOption: 'standard', voucherCodes: [] }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})

describe('CheckoutPage vouchers (specs/070)', () => {
  const quote = (discount = 0, vouchers: Quote['vouchers'] = []): Quote => ({
    items: [{
      productId: 'p', productName: 'Ricoh GR III', variantId: 'v', sku: null, optionSummary: null,
      quantity: 1, unitPrice: 1_000_000, totalPrice: 1_000_000, taxAmount: 100_000 - discount / 10, discount,
    }],
    shippingAddress: saved, shippingOption: { code: 'standard', name: 'Standard' },
    currency: 'VND', subtotal: 1_000_000, shippingPrice: 30_000, taxTotal: 103_000 - discount / 10, discountTotal: discount,
    taxRate: 0.1, totalAmount: 1_133_000 - discount - discount / 10, vouchers,
  })
  const sale = [{ code: 'SALE10', name: 'Ten off', isShop: false, sellerName: null, benefit: 'Percent', amount: 100_000 }]

  beforeEach(() => {
    vi.spyOn(Address, 'list').mockResolvedValue([saved])
  })

  it('tries a code with the server before keeping it, then quotes and places the order with it', async () => {
    const quoted = vi.spyOn(Order, 'quote').mockImplementation(async (choice) =>
      choice.voucherCodes?.includes('SALE10') ? quote(100_000, sale) : quote())
    const place = vi.spyOn(Order, 'place').mockResolvedValue({ orderId: 'o-1' } as OrderModel)
    const user = userEvent.setup()
    renderAsSeller(<CheckoutPage />, '/checkout')

    await user.type(await screen.findByLabelText('Voucher code'), ' sale10 ')
    await user.click(screen.getByRole('button', { name: 'Apply' }))

    expect(await screen.findByText('Voucher SALE10')).toBeInTheDocument()
    expect(quoted).toHaveBeenCalledWith({ addressId: 'a-new', shippingOption: 'standard', voucherCodes: ['SALE10'] })
    expect(screen.getByRole('list', { name: 'Applied vouchers' })).toHaveTextContent('SALE10')

    await user.click(screen.getByRole('button', { name: /Place order/ }))
    await waitFor(() => expect(place).toHaveBeenCalledWith({ addressId: 'a-new', shippingOption: 'standard', voucherCodes: ['SALE10'] }))
  })

  /** Research D1: a refused code is its own error, beside the box - the summary keeps its quote. */
  it('shows a refused code in the server words and keeps the summary as it was', async () => {
    vi.spyOn(Order, 'quote').mockImplementation(async (choice) => {
      if (choice.voucherCodes?.length) throw refusal(409, 'Voucher BIG needs an order of at least 5000000 VND on what it applies to.')
      return quote()
    })
    const user = userEvent.setup()
    renderAsSeller(<CheckoutPage />, '/checkout')

    await user.type(await screen.findByLabelText('Voucher code'), 'BIG')
    await user.click(screen.getByRole('button', { name: 'Apply' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('at least 5000000 VND')
    expect(screen.queryByRole('list', { name: 'Applied vouchers' })).not.toBeInTheDocument()
    expect(screen.getByText('Ricoh GR III')).toBeInTheDocument()
  })

  it('removes an applied code and quotes without it', async () => {
    const quoted = vi.spyOn(Order, 'quote').mockImplementation(async (choice) =>
      choice.voucherCodes?.includes('SALE10') ? quote(100_000, sale) : quote())
    const user = userEvent.setup()
    renderAsSeller(<CheckoutPage />, '/checkout')
    await user.type(await screen.findByLabelText('Voucher code'), 'SALE10')
    await user.click(screen.getByRole('button', { name: 'Apply' }))
    await screen.findByText('Voucher SALE10')

    await user.click(screen.getByRole('button', { name: 'Remove SALE10' }))

    await waitFor(() => expect(screen.queryByText('Voucher SALE10')).not.toBeInTheDocument())
    expect(quoted).toHaveBeenLastCalledWith({ addressId: 'a-new', shippingOption: 'standard', voucherCodes: [] })
  })
})

describe('CheckoutPage public vouchers (specs/114)', () => {
  it("offers the platform's and the cart's shops' vouchers, and Use tries the code like typing it", async () => {
    vi.spyOn(Address, 'list').mockResolvedValue([saved])
    const line = {
      productId: 'p', productName: 'Ricoh GR III', variantId: 'v', sku: null, optionSummary: null, sellerId: 's1',
      quantity: 1, unitPrice: 1_000_000, totalPrice: 1_000_000, taxAmount: 100_000, discount: 0,
    }
    const base: Quote = {
      items: [line], shippingAddress: saved, shippingOption: { code: 'standard', name: 'Standard' }, currency: 'VND',
      subtotal: 1_000_000, shippingPrice: 30_000, taxTotal: 103_000, discountTotal: 0, taxRate: 0.1, totalAmount: 1_133_000, vouchers: [],
    }
    const quoted = vi.spyOn(Order, 'quote').mockResolvedValue(base)
    const asked = vi.spyOn(Voucher, 'public').mockResolvedValue([{
      code: 'MAI10', name: 'Mai ten', isPlatform: false, sellerId: 's1', benefit: 'FixedAmount', percent: null, currency: 'VND',
      fixedValue: 50_000, maxDiscount: null, minSubtotal: null, endsAt: null, conditions: [], targeted: false,
    }])
    const user = userEvent.setup()
    renderAsSeller(<CheckoutPage />, '/checkout')

    await user.click(await screen.findByRole('button', { name: 'Use' }))

    expect(asked).toHaveBeenCalledWith({ platform: true, sellerIds: ['s1'] })
    await waitFor(() => expect(quoted).toHaveBeenCalledWith({ addressId: 'a-new', shippingOption: 'standard', voucherCodes: ['MAI10'] }))
  })
})

describe('CheckoutPage: delivery time and payment (specs/134, #253)', () => {
  it('says how long each option takes, and nothing for one that does not say', async () => {
    vi.spyOn(Address, 'list').mockResolvedValue([saved])
    vi.spyOn(Order, 'shippingOptions').mockResolvedValue([
      { code: 'standard', name: 'Standard', price: 30000, currency: 'VND', minDays: 3, maxDays: 5 },
      { code: 'express', name: 'Express', price: 60000, currency: 'VND', minDays: 1, maxDays: 1 },
      { code: 'pickup', name: 'Pick up', price: 0, currency: 'VND', minDays: null, maxDays: null },
    ])
    renderAsSeller(<CheckoutPage />, '/checkout')

    expect(await screen.findByText('3–5 business days')).toBeInTheDocument()
    expect(screen.getByText('1 business day')).toBeInTheDocument()
    expect(screen.getByText('Pick up').parentElement).not.toHaveTextContent(/day/)
  })

  it('says how payment works, and that no money moves while Payment says it is the stand-in', async () => {
    vi.spyOn(Address, 'list').mockResolvedValue([saved])
    vi.spyOn(Payment, 'about').mockResolvedValue({ movesMoney: false, atGateway: false })
    renderAsSeller(<CheckoutPage />, '/checkout')

    expect(await screen.findByText('Your order is charged once, in full, when you place it.')).toBeInTheDocument()
    expect(await screen.findByRole('note')).toHaveTextContent('no money is moved')
  })

  it('says nothing about the provider when Payment is a real one or cannot be asked', async () => {
    vi.spyOn(Address, 'list').mockResolvedValue([saved])
    const stub = vi.spyOn(Payment, 'about').mockResolvedValue({ movesMoney: true, atGateway: false })
    renderAsSeller(<CheckoutPage />, '/checkout')

    expect(await screen.findByText('Your order is charged once, in full, when you place it.')).toBeInTheDocument()
    await waitFor(() => expect(stub).toHaveBeenCalled())
    expect(screen.queryByText(/no money is moved/)).not.toBeInTheDocument()
  })

  it("says the customer pays at VNPay after placing the order, when Payment sends them there (specs/143)", async () => {
    vi.spyOn(Address, 'list').mockResolvedValue([saved])
    vi.spyOn(Payment, 'about').mockResolvedValue({ movesMoney: false, atGateway: true })
    renderAsSeller(<CheckoutPage />, '/checkout')

    expect(await screen.findByText(/pay on VNPay's page/)).toBeInTheDocument()
    expect(screen.queryByText('Your order is charged once, in full, when you place it.')).not.toBeInTheDocument()
    expect(await screen.findByRole('note')).toHaveTextContent('no money is moved')
  })
})
