import { TruckIcon } from 'lucide-react'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { ShippingOption } from '@/services/order/types'
import { Price } from '@/components/shared/price'
import { cn } from '@/utils/shared'

/** The delivery options and what each costs - decided by Order, never by the client. */
export function DeliveryChoice({
  options,
  shippingOption,
  onChange,
}: {
  options: ShippingOption[]
  shippingOption: string | null
  onChange: (code: string) => void
}) {
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
            <span className="flex-1 font-medium">{option.name}</span>
            <Price value={option.price} currency={option.currency} className="font-semibold" />
          </Label>
        )
      })}
    </RadioGroup>
  )
}
