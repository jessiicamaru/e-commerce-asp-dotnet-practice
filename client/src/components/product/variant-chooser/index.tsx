import { useTranslation } from 'react-i18next'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import type { Variant } from '@/services/product/types'
import { money } from '@/utils/shared'

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
    <fieldset className="rounded-lg border p-4">
      <legend className="px-1 text-sm font-medium">{t('product.chooseOne')}</legend>
      <RadioGroup value={selectedId ?? ''} onValueChange={onSelect} className="gap-3">
        {sellable.map((variant) => (
          <div key={variant.id} className="flex items-start gap-3">
            <RadioGroupItem value={variant.id} id={`variant-${variant.id}`} className="mt-1" />
            <Label htmlFor={`variant-${variant.id}`} className="flex flex-col items-start gap-0.5 font-normal">
              <span>
                {variant.optionSummary || variant.sku} · <span className="font-semibold">{money(variant.price)}</span>
              </span>
              <span className="text-muted-foreground text-xs">
                {variant.availability === 'InStock' ? t('product.inStock') : t('product.outOfStock')} · {variant.sku}
              </span>
            </Label>
          </div>
        ))}
      </RadioGroup>
    </fieldset>
  )
}
