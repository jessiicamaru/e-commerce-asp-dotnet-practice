import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { ShippingOption } from '@/services/order/types'
import { money } from '@/utils/shared'

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
    <fieldset className="rounded-lg border p-4">
      <legend className="px-1 text-sm font-medium">Delivery</legend>
      <RadioGroup value={shippingOption ?? ''} onValueChange={onChange} className="gap-3">
        {options.map((option) => (
          <div key={option.code} className="flex items-center gap-3">
            <RadioGroupItem value={option.code} id={`shipping-${option.code}`} />
            <Label htmlFor={`shipping-${option.code}`} className="font-normal">
              {option.name} · {money(option.price)}
            </Label>
          </div>
        ))}
      </RadioGroup>
    </fieldset>
  )
}
