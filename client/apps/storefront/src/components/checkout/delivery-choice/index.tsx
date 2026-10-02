import { TruckIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Label } from '@ecommerce/ui/label'
import { RadioGroup, RadioGroupItem } from '@ecommerce/ui/radio-group'
import type { ShippingOption } from '@ecommerce/core/services/order/types'
import { Price } from '@/components/shared/price'
import { cn } from 'cn'

/**
 * The delivery options, what each costs and how long it takes - decided by Order, never by the client. An option that
 * says no time shows none, never "0 days" (specs/134, #253).
 */
export function DeliveryChoice({
  options,
  shippingOption,
  onChange,
}: {
  options: ShippingOption[]
  shippingOption: string | null
  onChange: (code: string) => void
}) {
  const { t } = useTranslation('checkout')
  return (
    <RadioGroup value={shippingOption ?? ''} onValueChange={onChange} className="gap-2">
      {options.map((option) => {
        const chosen = option.code === shippingOption

        return (
          <Label
            key={option.code}
            htmlFor={`shipping-${option.code}`}
            className={cn(
              'flex cursor-pointer items-center gap-3 rounded-2xl px-4 py-3 font-normal ring-1 transition-colors',
              chosen ? 'bg-accent ring-primary' : 'bg-card ring-border/60 hover:ring-primary/50',
            )}
          >
            <RadioGroupItem value={option.code} id={`shipping-${option.code}`} className="shrink-0" />
            <TruckIcon className="text-muted-foreground size-4.5" />
            <span className="grid flex-1">
              <span className="font-medium">{option.name}</span>
              {option.minDays != null && option.maxDays != null && (
                <span className="text-muted-foreground text-xs">
                  {option.minDays === option.maxDays
                    ? t('deliveryDays', { count: option.minDays })
                    : t('deliveryDaysRange', { min: option.minDays, max: option.maxDays })}
                </span>
              )}
            </span>
            <Price value={option.price} currency={option.currency} className="font-semibold" />
          </Label>
        )
      })}
    </RadioGroup>
  )
}
