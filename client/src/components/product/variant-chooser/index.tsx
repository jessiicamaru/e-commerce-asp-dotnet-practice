import { useTranslation } from 'react-i18next'
import { StockBadge } from '@/components/product/stock-badge'
import { Price } from '@/components/shared/price'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { Variant } from '@/services/product/types'
import type { Stock } from '@/services/stock/types'
import { cn } from '@/utils/shared'

/**
 * Which shape of the product to buy (specs/020).
 *
 * Nothing is preselected when there is a choice: defaulting to the first one sells somebody a kit they
 * did not pick. A product with exactly one variant has no choice to make, so this renders nothing and
 * the page uses that variant.
 */
export function VariantChooser({
  variants,
  selectedId,
  onSelect,
  stock = {},
  stockPending = false,
}: {
  variants: Variant[]
  selectedId: string | null
  onSelect: (variantId: string) => void
  /** Inventory's count per variant id, when known - "3 left" says more than "in stock". */
  stock?: Record<string, Stock | null>
  stockPending?: boolean
}) {
  const { t } = useTranslation('catalog')
  const sellable = variants.filter((variant) => variant.isActive)

  if (sellable.length <= 1) {
    return null
  }

  return (
    <fieldset>
      <legend className="mb-2 text-sm font-medium">{t('product.chooseOne')}</legend>

      {/* Each choice is a card the whole of which is clickable, not a dot with words beside it.
          The radio is still there and still labelled - it is what keyboards and screen readers
          use - it is simply not the only target a pointer has. It is centred on the card, not
          pinned to its first line: pinned, it sat visibly above the text beside it. */}
      <RadioGroup value={selectedId ?? ''} onValueChange={onSelect} className="gap-2">
        {sellable.map((variant) => {
          const chosen = variant.id === selectedId

          return (
            <Label
              key={variant.id}
              htmlFor={`variant-${variant.id}`}
              className={cn(
                'flex cursor-pointer items-center gap-3 rounded-2xl px-4 py-3 font-normal ring-1 transition-colors',
                chosen ? 'bg-accent ring-primary' : 'bg-card ring-border/60 hover:ring-primary/50',
              )}
            >
              <RadioGroupItem value={variant.id} id={`variant-${variant.id}`} className="shrink-0" />

              <span className="flex min-w-0 flex-1 flex-wrap items-center justify-between gap-x-3 gap-y-1">
                <span className="grid min-w-0 gap-1">
                  <span className="font-medium">{variant.optionSummary || variant.sku}</span>
                  <span className="text-muted-foreground font-mono text-xs">{variant.sku}</span>
                </span>
                <span className="grid justify-items-end gap-1">
                  <Price value={variant.price} currency={variant.currency} className="font-semibold" />
                  <StockBadge available={stock[variant.id]?.quantityAvailable} pending={stockPending} />
                </span>
              </span>
            </Label>
          )
        })}
      </RadioGroup>
    </fieldset>
  )
}
