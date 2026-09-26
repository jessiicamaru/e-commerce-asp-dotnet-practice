import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@/config/i18n'
import { Product } from '@/services/product'
import { Voucher } from '@/services/voucher'
import type { VoucherSummary } from '@/services/voucher/types'
import { refusal } from '@/test/refusal'
import { renderAsAdmin, renderAsSeller } from '@/test/render'
import { AdminVouchersPage } from '@/pages/admin-vouchers'
import { ShopVouchersPage } from '.'

const sale: VoucherSummary = {
  id: 'v-1', code: 'MAI10', name: 'Mai ten', isPlatform: false, benefit: 'Percent', percent: 10, status: 'Active',
  startsAt: '2026-09-26T00:00:00Z', endsAt: null, totalLimit: 100, usedCount: 3, perCustomerLimit: 1,
  amounts: [{ currency: 'VND', fixedValue: null, maxDiscount: 100_000, minSubtotal: 500_000 }],
  conditions: [{ type: 'FirstOrderInShop', value: null }], targets: [], createdAt: '',
}

const page = (items: VoucherSummary[]) => ({ items, page: 1, pageSize: 12, totalCount: items.length })

beforeEach(async () => {
  await i18n.changeLanguage('en')
  vi.spyOn(Product, 'mine').mockResolvedValue({ items: [{ id: 'p1', name: 'Viltrox 56mm' }], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false } as never)
  vi.spyOn(Product, 'list').mockResolvedValue({ items: [{ id: 'p9', name: 'Sony A7 IV' }], pageNumber: 1, totalPages: 1, totalCount: 1, hasPreviousPage: false, hasNextPage: false } as never)
})

describe('ShopVouchersPage (specs/070)', () => {
  it('lists the seller vouchers in words', async () => {
    vi.spyOn(Voucher, 'mine').mockResolvedValue(page([sale]))
    renderAsSeller(<ShopVouchersPage />, '/shop/vouchers')

    expect(await screen.findByText('MAI10')).toBeInTheDocument()
    expect(screen.getByText('10% off · up to ₫100,000 · on orders from ₫500,000')).toBeInTheDocument()
    expect(screen.getByText('First order in this shop · Everything')).toBeInTheDocument()
    expect(screen.getByText(/Used 3 of 100 · once per customer/)).toBeInTheDocument()
  })

  it("creates one for the shop - no free delivery offered - with the seller's own product", async () => {
    vi.spyOn(Voucher, 'mine').mockResolvedValue(page([]))
    const create = vi.spyOn(Voucher, 'create').mockResolvedValue(sale)
    const user = userEvent.setup()
    renderAsSeller(<ShopVouchersPage />, '/shop/vouchers')

    await user.click(await screen.findByRole('button', { name: /New voucher/ }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).queryByText('Free delivery')).not.toBeInTheDocument()
    expect(within(dialog).queryByText('New customers only')).not.toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Code'), 'mai10')
    await user.type(within(dialog).getByLabelText('Name'), 'Mai ten')
    await user.type(within(dialog).getByLabelText('At most (VND)'), '100000')
    await user.click(within(dialog).getByText('First order in my shop only'))
    await user.click(await within(dialog).findByText('Viltrox 56mm'))
    await user.click(within(dialog).getByRole('button', { name: 'Create voucher' }))

    await waitFor(() => expect(create).toHaveBeenCalled())
    expect(create.mock.calls[0][0]).toMatchObject({
      code: 'MAI10', name: 'Mai ten', benefit: 'Percent', percent: 10,
      amounts: [{ currency: 'VND', fixedValue: null, maxDiscount: 100_000, minSubtotal: null }],
      conditions: [{ type: 'FirstOrderInShop', value: null }],
      targets: [{ type: 'Product', id: 'p1' }],
    })
    expect(Product.list).not.toHaveBeenCalled()   // a seller picks from their own listings only
  })

  it('shows a refusal in the server words inside the form', async () => {
    vi.spyOn(Voucher, 'mine').mockResolvedValue(page([]))
    vi.spyOn(Voucher, 'create').mockRejectedValue(refusal(409, 'The code MAI10 is taken.'))
    const user = userEvent.setup()
    renderAsSeller(<ShopVouchersPage />, '/shop/vouchers')

    await user.click(await screen.findByRole('button', { name: /New voucher/ }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText('Code'), 'MAI10')
    await user.type(within(dialog).getByLabelText('Name'), 'Again')
    await user.click(within(dialog).getByRole('button', { name: 'Create voucher' }))

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('The code MAI10 is taken.')
  })

  it('disables one only after confirming', async () => {
    vi.spyOn(Voucher, 'mine').mockResolvedValue(page([sale]))
    const disable = vi.spyOn(Voucher, 'disable').mockResolvedValue({ ...sale, status: 'Disabled' })
    const user = userEvent.setup()
    renderAsSeller(<ShopVouchersPage />, '/shop/vouchers')

    await user.click(await screen.findByRole('button', { name: /Disable/ }))
    expect(disable).not.toHaveBeenCalled()
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Disable' }))

    await waitFor(() => expect(disable).toHaveBeenCalledWith('v-1'))
  })
})

describe('AdminVouchersPage (specs/070)', () => {
  it('offers free delivery and new customers, and picks from the whole catalogue', async () => {
    vi.spyOn(Voucher, 'mine').mockResolvedValue(page([]))
    const create = vi.spyOn(Voucher, 'create').mockResolvedValue({ ...sale, isPlatform: true })
    const user = userEvent.setup()
    renderAsAdmin(<AdminVouchersPage />, '/admin/vouchers')

    await user.click(await screen.findByRole('button', { name: /New voucher/ }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('Sony A7 IV')).toBeInTheDocument()
    expect(within(dialog).queryByText('First order in my shop only')).not.toBeInTheDocument()

    await user.type(within(dialog).getByLabelText('Code'), 'FREESHIP')
    await user.type(within(dialog).getByLabelText('Name'), 'Free delivery')
    await user.click(within(dialog).getByText('Free delivery', { selector: 'label' }))
    await user.click(within(dialog).getByText('New customers only'))
    await user.click(within(dialog).getByRole('button', { name: 'Create voucher' }))

    await waitFor(() => expect(create).toHaveBeenCalled())
    expect(create.mock.calls[0][0]).toMatchObject({ benefit: 'FreeShipping', percent: null, conditions: [{ type: 'NewCustomer', value: null }], targets: [] })
    expect(Product.mine).not.toHaveBeenCalled()
  })
})
