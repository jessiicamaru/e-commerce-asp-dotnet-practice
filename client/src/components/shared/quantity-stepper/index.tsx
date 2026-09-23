import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { MinusIcon, PlusIcon } from 'lucide-react'
import { InputGroup, InputGroupAddon, InputGroupButton, InputGroupInput } from '@/components/ui/input-group'
import { cn } from '@/utils/shared'
import { clampQuantity } from './clamp'

/**
 * A quantity with a minus and a plus either side - what people expect from a shop, instead of a
 * browser number field with two arrows the size of a grain of rice.
 *
 * <p>
 * The buttons change it at once. Typing is committed on Enter or when the field is left, so "12"
 * does not send "1" first. `max`, when given, is Inventory's available count: the plus stops there,
 * rather than letting somebody add a quantity checkout will refuse.
 * </p>
 */
export function QuantityStepper({
  value,
  onChange,
  min = 1,
  max,
  disabled = false,
  label,
  size = 'default',
}: {
  value: number
  onChange: (value: number) => void
  min?: number
  max?: number
  disabled?: boolean
  /** The accessible name of the number field, e.g. "Quantity: Sony A7 IV". */
  label: string
  size?: 'sm' | 'default'
}) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState<string | null>(null)

  function commit(raw: string) {
    setDraft(null)
    const next = clampQuantity(raw, min, max)
    if (next !== null && next !== value) onChange(next)
  }

  return (
    <InputGroup className={cn('w-fit rounded-full', size === 'sm' ? 'h-8' : 'h-10')}>
      <InputGroupAddon>
        <InputGroupButton
          size="icon-xs"
          className="rounded-full"
          aria-label={t('quantity.decrease')}
          disabled={disabled || value <= min}
          onClick={() => onChange(value - 1)}
        >
          <MinusIcon />
        </InputGroupButton>
      </InputGroupAddon>
      <InputGroupInput
        inputMode="numeric"
        aria-label={label}
        disabled={disabled}
        className={cn('text-center font-semibold tabular-nums', size === 'sm' ? 'w-9' : 'w-11')}
        value={draft ?? String(value)}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={(event) => commit(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault()
            commit(event.currentTarget.value)
          }
          if (event.key === 'Escape') setDraft(null)
        }}
      />
      <InputGroupAddon align="inline-end">
        <InputGroupButton
          size="icon-xs"
          className="rounded-full"
          aria-label={t('quantity.increase')}
          disabled={disabled || (max !== undefined && value >= max)}
          onClick={() => onChange(value + 1)}
        >
          <PlusIcon />
        </InputGroupButton>
      </InputGroupAddon>
    </InputGroup>
  )
}
