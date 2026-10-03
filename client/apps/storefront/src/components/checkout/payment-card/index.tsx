import { CreditCardIcon, FlaskConicalIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardHeader, CardTitle } from '@ecommerce/ui/card'
import { usePaymentAbout } from '@ecommerce/core/hooks/payment'

/**
 * How payment works, before the customer commits (specs/134, #253): charged once, in full, when the order is placed; a
 * refusal takes nothing and puts the goods back; a cancellation is refunded in full. While Payment says it is the
 * stand-in, the card says plainly that no money is moved - and says nothing either way when Payment cannot be asked.
 * With VNPay (specs/143) the customer pays on the gateway's page after placing the order, and the card says that instead.
 */
export function PaymentCard() {
  const { t } = useTranslation('checkout')
  const about = usePaymentAbout()

  return (
    <Card className="rounded-3xl">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <CreditCardIcon className="text-muted-foreground size-4.5" />
          {t('payment.title')}
        </CardTitle>
      </CardHeader>
      <CardContent className="grid gap-2 text-sm">
        <p>{about.data?.atGateway ? t('payment.atGateway') : t('payment.charged')}</p>
        <p className="text-muted-foreground">{t('payment.refused')}</p>
        <p className="text-muted-foreground">{t('payment.refunded')}</p>
        {about.data?.movesMoney === false && (
          <p role="note" className="bg-accent text-accent-foreground mt-1 flex items-start gap-2 rounded-2xl px-3 py-2">
            <FlaskConicalIcon className="mt-0.5 size-4 shrink-0" />
            {t('payment.stub')}
          </p>
        )}
      </CardContent>
    </Card>
  )
}
