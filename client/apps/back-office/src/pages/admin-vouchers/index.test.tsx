import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Product } from '@ecommerce/core/services/product'
import { Voucher } from '@ecommerce/core/services/voucher'
import type { VoucherSummary } from '@ecommerce/core/services/voucher/types'
import { renderAsAdmin } from '@ecommerce/core/test/render'
import { AdminVouchersPage } from '.'

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

describe('vouchers searched and filtered (specs/133, #249)', () => {
  it('asks for the code or name typed, and the state chosen, each tab saying how many', async () => {
    const mine = vi.spyOn(Voucher, 'mine').mockImplementation(async (_page, size, filter = {}) =>
      size === 1 ? ({ items: [], page: 1, pageSize: 1, totalCount: filter.state === 'Ended' ? 4 : filter.state ? 1 : 6 }) : page([sale]),
    )
    const user = userEvent.setup()
    renderAsAdmin(<AdminVouchersPage />, '/admin/vouchers')

    expect(await screen.findByRole('tab', { name: 'Ended 4' })).toBeInTheDocument()
    await user.type(screen.getByRole('textbox', { name: 'Code or name' }), 'mai{Enter}')
    await waitFor(() => expect(mine).toHaveBeenCalledWith(1, 12, { search: 'mai', state: '' }))

    await user.click(screen.getByRole('tab', { name: /Ended/ }))
    await waitFor(() => expect(mine).toHaveBeenCalledWith(1, 12, { search: 'mai', state: 'Ended' }))
    expect(mine).toHaveBeenCalledWith(1, 1, { search: 'mai', state: 'Ended' })
  })
})
