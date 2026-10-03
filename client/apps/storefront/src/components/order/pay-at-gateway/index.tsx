import { ExternalLinkIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { buttonVariants } from '@ecommerce/ui/button'
import { cn } from 'cn'
import type { PaymentCheckout } from '@ecommerce/core/services/payment/types'

/**
 * The way to pay while an order waits at a gateway (specs/143): a link to VNPay's page, signed by Payment a moment ago,
 * and until when it works. A full-page link, not a fetch - the customer leaves the shop and VNPay sends them back.
 * Nothing at all for a payment that is decided, still being prepared, or taken by the stub.
 */
export function PayAtGateway({ checkout }: { checkout: PaymentCheckout | undefined }) {
  const { t, i18n } = useTranslation('orders')

  if (checkout?.state === 'Expired') {
    return <p className="text-muted-foreground mt-3 text-sm">{t('pay.expired')}</p>
  }

  if (checkout?.state !== 'AwaitingPayment' || !checkout.payUrl) {
    return null
  }

  const until = checkout.expiresAt
    ? new Date(checkout.expiresAt).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' })
    : ''

  return (
    <div className="mt-3 grid gap-2">
      <p className="text-sm">{t('pay.waiting', { time: until })}</p>
      <p>
        <a href={checkout.payUrl} className={cn(buttonVariants(), 'rounded-full px-5 font-semibold')}>
          {t('pay.button')}
          <ExternalLinkIcon className="size-4" aria-hidden="true" />
        </a>
      </p>
    </div>
  )
}
