import { useTranslation } from 'react-i18next'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { Variant } from '@/services/product/types'
import { Price } from '@/components/shared/price'
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
}: {
  variants: Variant[]
  selectedId: string | null
  onSelect: (variantId: string) => void
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
          use - it is simply not the only target a pointer has. */}
      <RadioGroup value={selectedId ?? ''} onValueChange={onSelect} className="gap-2">
        {sellable.map((variant) => {
          const chosen = variant.id === selectedId

          return (
            <Label
              key={variant.id}
              htmlFor={`variant-${variant.id}`}
              className={cn(
                'flex cursor-pointer items-start gap-3 rounded-2xl p-3 font-normal ring-1 transition-colors',
                chosen ? 'bg-accent ring-primary' : 'bg-card ring-border/60 hover:ring-primary/50',
              )}
            >
              <RadioGroupItem value={variant.id} id={`variant-${variant.id}`} className="mt-0.5" />

              <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="flex flex-wrap items-baseline justify-between gap-2">
                  <span className="font-medium">{variant.optionSummary || variant.sku}</span>
                  <Price value={variant.price} currency={variant.currency} className="font-semibold" />
                </span>
                <span className="text-muted-foreground text-xs">
                  {variant.availability === 'InStock' ? t('product.inStock') : t('product.outOfStock')} · {variant.sku}
                </span>
              </span>
            </Label>
          )
        })}
      </RadioGroup>
    </fieldset>
  )
}
