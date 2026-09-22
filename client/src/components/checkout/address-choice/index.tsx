import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { Address } from '@/services/address/types'
import { describeAddress } from '@/utils/address'

export function AddressChoice({
  addresses,
  addressId,
  onChange,
}: {
  addresses: Address[]
  addressId: string | null
  onChange: (addressId: string) => void
}) {
  const { t } = useTranslation('checkout')

  return (
    <fieldset className="rounded-lg border p-4">
      <legend className="px-1 text-sm font-medium">{t('deliverTo')}</legend>
      <RadioGroup value={addressId ?? ''} onValueChange={onChange} className="gap-3">
        {addresses.map((address) => (
          <div key={address.id} className="flex items-start gap-3">
            <RadioGroupItem value={address.id} id={`address-${address.id}`} className="mt-1" />
            <Label htmlFor={`address-${address.id}`} className="flex flex-col items-start gap-1 font-normal">
              <span className="flex items-center gap-2 font-medium">
                {address.recipientName}
                {address.isDefault && <Badge variant="secondary">{t('default')}</Badge>}
              </span>
              <span className="text-muted-foreground text-xs">{describeAddress(address)}</span>
            </Label>
          </div>
        ))}
      </RadioGroup>
      <Link to="/addresses" className="text-muted-foreground mt-3 inline-block text-xs underline">
        {t('manageAddresses')}
      </Link>
    </fieldset>
  )
}
