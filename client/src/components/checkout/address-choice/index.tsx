import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { PlusIcon } from 'lucide-react'
import { AddressDialog } from '@/components/address/address-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { Address } from '@/services/address/types'
import { describeAddress } from '@/utils/address'
import { cn } from '@/utils/shared'

/**
 * Where the order goes. A new address is added here, in a dialog, and chosen as soon as it is saved -
 * checkout used to send somebody off to another page for this and leave them to find their way back.
 */
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
    <RadioGroup value={addressId ?? ''} onValueChange={onChange} className="gap-2">
      {addresses.map((address) => {
        const chosen = address.id === addressId

        return (
          <Label
            key={address.id}
            htmlFor={`address-${address.id}`}
            className={cn(
              'flex cursor-pointer items-center gap-3 rounded-2xl px-4 py-3 font-normal ring-1 transition-colors',
              chosen ? 'bg-accent ring-primary' : 'bg-card ring-border/60 hover:ring-primary/50',
            )}
          >
            <RadioGroupItem value={address.id} id={`address-${address.id}`} className="shrink-0" />
            <span className="grid min-w-0 gap-0.5">
              <span className="flex items-center gap-2 font-medium">
                {address.recipientName}
                {address.isDefault && <Badge variant="secondary">{t('default')}</Badge>}
              </span>
              <span className="text-muted-foreground text-sm">{describeAddress(address)}</span>
            </span>
          </Label>
        )
      })}

      <div className="flex flex-wrap items-center justify-between gap-2 pt-1">
        <AddressDialog
          onSaved={(address) => onChange(address.id)}
          trigger={
            <Button variant="outline" size="sm" className="h-9 rounded-full px-3">
              <PlusIcon /> {t('newAddress')}
            </Button>
          }
        />
        <Link to="/addresses" className="text-muted-foreground hover:text-foreground text-xs underline">
          {t('manageAddresses')}
        </Link>
      </div>
    </RadioGroup>
  )
}
