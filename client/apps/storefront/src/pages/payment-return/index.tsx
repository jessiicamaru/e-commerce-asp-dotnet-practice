import { useTranslation } from 'react-i18next'
import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { orderIdFromReference } from '@ecommerce/core/utils/payment'

/**
 * Where VNPay sends the customer back (specs/143). It reads ONE thing from the address - which order - and shows that
 * order as the shop records it. The outcome VNPay wrote into the address (`vnp_ResponseCode`) is never read: anybody
 * can type an address, and only the gateway's signed call to the shop decides whether an order was paid.
 */
export function PaymentReturnPage() {
  const { t } = useTranslation('orders')
  const [params] = useSearchParams()
  const orderId = orderIdFromReference(params.get('vnp_TxnRef'))

  if (!orderId) {
    return (
      <section className="grid gap-3">
        <p role="alert">{t('pay.returnUnknown')}</p>
        <p>
          <Link to="/orders" className="underline">
            {t('pay.toOrders')}
          </Link>
        </p>
      </section>
    )
  }

  return <Navigate to={`/orders/${orderId}`} replace />
}
