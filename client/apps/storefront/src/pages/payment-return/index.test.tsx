import { screen } from '@testing-library/react'
import { Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import i18n from '@ecommerce/core/config/i18n'
import { renderAsSeller } from '@ecommerce/core/test/render'
import { PaymentReturnPage } from '.'

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>
}

function renderAt(path: string) {
  return renderAsSeller(
    <Routes>
      <Route path="/payment/vnpay-return" element={<PaymentReturnPage />} />
      <Route path="/orders/:id" element={<Where />} />
    </Routes>,
    path,
  )
}

beforeEach(async () => {
  await i18n.changeLanguage('en')
})

describe('PaymentReturnPage (specs/143)', () => {
  it("shows the order VNPay's reference names", async () => {
    renderAt('/payment/vnpay-return?vnp_TxnRef=0199a1b2c3d4e5f60718293a4b5c6d7e&vnp_ResponseCode=00&vnp_SecureHash=x')

    expect(await screen.findByTestId('where')).toHaveTextContent('/orders/0199a1b2-c3d4-e5f6-0718-293a4b5c6d7e')
  })

  it('never takes the outcome from the address: a "paid" code changes nothing it shows', async () => {
    // The same order whatever the code says - the order page reads the shop's own record.
    renderAt('/payment/vnpay-return?vnp_TxnRef=0199a1b2c3d4e5f60718293a4b5c6d7e&vnp_ResponseCode=24')

    expect(await screen.findByTestId('where')).toHaveTextContent('/orders/0199a1b2-c3d4-e5f6-0718-293a4b5c6d7e')
    expect(screen.queryByText(/paid|cancel/i)).not.toBeInTheDocument()
  })

  it('says so for a reference that is not one of this shop', async () => {
    renderAt('/payment/vnpay-return?vnp_TxnRef=..%2F..%2Fadmin')

    expect(await screen.findByRole('alert')).toHaveTextContent('does not name one of your orders')
    expect(screen.queryByTestId('where')).not.toBeInTheDocument()
  })
})
