import { screen, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { SellerLayout } from '@/layouts/seller-layout'
import { Order } from '@ecommerce/core/services/order'
import { Questions } from '@ecommerce/core/services/question'
import { Seller } from '@ecommerce/core/services/seller'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { Route, Routes } from 'react-router-dom'
import { NeedsYou } from '.'

const page = (totalCount: number) => ({ items: [], page: 1, pageSize: 1, pageNumber: 1, totalPages: 0, totalCount, hasPreviousPage: false, hasNextPage: false }) as never

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('NeedsYou (specs/131, #247)', () => {
  it('lists what waits, each linking to where it is done', () => {
    renderAsSeller(<NeedsYou waiting={{ toPrepare: 2, questions: 1, returns: 0, payoutAccount: false }} />)

    const panel = screen.getByRole('region', { name: 'Needs you' })
    expect(within(panel).getByRole('link', { name: /2 sales to prepare/ })).toHaveAttribute('href', '/shop/sales')
    expect(within(panel).getByRole('link', { name: /1 question to answer/ })).toHaveAttribute('href', '/shop/questions')
    expect(within(panel).getByRole('link', { name: /Add the account your payouts go to/ })).toHaveAttribute('href', '/shop/payouts')
    expect(within(panel).queryByText(/return/)).not.toBeInTheDocument()
  })

  it('says nothing waits rather than vanishing', () => {
    renderAsSeller(<NeedsYou waiting={{ toPrepare: 0, questions: 0, returns: 0, payoutAccount: true }} />)

    expect(screen.getByText('Nothing is waiting for you.')).toBeInTheDocument()
  })
})

describe('the seller menu shows what waits (specs/131)', () => {
  it('counts sales to prepare, questions and returns - asked of each owner, a page of one', async () => {
    const sales = vi.spyOn(Order, 'sales').mockResolvedValue(page(3))
    vi.spyOn(Questions, 'toAnswer').mockResolvedValue(page(2))
    vi.spyOn(Order, 'saleReturns').mockResolvedValue(page(1))
    vi.spyOn(Seller, 'payoutAccount').mockResolvedValue(null)
    vi.spyOn(Seller, 'me').mockResolvedValue({ sellerId: 's1', shopName: 'Mai Lens', description: '' } as never)

    renderAsSeller(
      <Routes>
        <Route path="/shop" element={<SellerLayout />}>
          <Route index element={<p>home</p>} />
        </Route>
      </Routes>,
      '/shop',
    )

    expect(await within(screen.getByRole('link', { name: /Sales/ })).findByLabelText('3 waiting')).toBeInTheDocument()
    expect(await within(screen.getByRole('link', { name: /Questions/ })).findByLabelText('2 waiting')).toBeInTheDocument()
    expect(await within(screen.getByRole('link', { name: /Returns/ })).findByLabelText('1 waiting')).toBeInTheDocument()
    expect(sales).toHaveBeenCalledWith(1, 1, 'Paid')
  })
})
