import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { Voucher } from '@ecommerce/core/services/voucher'
import type { PublicVoucher } from '@ecommerce/core/services/voucher/types'
import { renderSignedOut } from '@ecommerce/core/test/render'
import { PublicVouchers } from '.'

const offer = (over: Partial<PublicVoucher> = {}): PublicVoucher => ({
  code: 'MAI10', name: 'Mai ten', isPlatform: false, sellerId: 's1', benefit: 'Percent', percent: 10, currency: 'VND',
  fixedValue: null, maxDiscount: 100_000, minSubtotal: 500_000, endsAt: '2026-10-31T17:00:00Z',
  conditions: [{ type: 'FirstOrderInShop', value: null }], targeted: false, ...over,
})

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('PublicVouchers (specs/114)', () => {
  it('asks for the scope it is given and words each voucher', async () => {
    const asked = vi.spyOn(Voucher, 'public').mockResolvedValue([offer(), offer({ code: 'LENS', percent: 20, targeted: true, conditions: [] })])
    renderSignedOut(<PublicVouchers scope={{ sellerIds: ['s1'] }} />)

    expect(await screen.findByText('MAI10')).toBeInTheDocument()
    expect(asked).toHaveBeenCalledWith({ sellerIds: ['s1'] })
    expect(screen.getByText('10% off · up to ₫100,000 · on orders from ₫500,000')).toBeInTheDocument()
    expect(screen.getByText(/First order in this shop · Everything/)).toBeInTheDocument()
    expect(screen.getByText(/1 product/)).toBeInTheDocument()
  })

  it('draws nothing when there is nothing to show, and asks nothing without a scope', async () => {
    const asked = vi.spyOn(Voucher, 'public').mockResolvedValue([])
    const { container } = renderSignedOut(<PublicVouchers scope={{ sellerIds: ['s1'] }} />)
    await vi.waitFor(() => expect(asked).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()

    asked.mockClear()
    renderSignedOut(<PublicVouchers scope={{ sellerIds: [] }} />)
    renderSignedOut(<PublicVouchers scope={null} />)
    expect(asked).not.toHaveBeenCalled()
  })

  it('at checkout offers each unused one to use, by its code', async () => {
    vi.spyOn(Voucher, 'public').mockResolvedValue([offer(), offer({ code: 'ALREADY' })])
    const use = vi.fn()
    const user = userEvent.setup()
    renderSignedOut(<PublicVouchers scope={{ platform: true }} onUse={use} usedCodes={['ALREADY']} />)

    await user.click(await screen.findByRole('button', { name: 'Use' }))

    expect(use).toHaveBeenCalledWith('MAI10')
    expect(screen.queryByText('ALREADY')).not.toBeInTheDocument()
  })
})
