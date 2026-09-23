import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImageOffIcon, SaveIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ProductImage } from '@/components/product/product-image'
import { StockBadge } from '@/components/product/stock-badge'
import { ImageDropzone } from '@/components/shared/image-dropzone'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { InputGroup, InputGroupAddon, InputGroupInput, InputGroupText } from '@/components/ui/input-group'
import { Label } from '@/components/ui/label'
import { CURRENCIES } from '@/config/money'
import { useRemoveVariantImage, useSetVariantPrice, useUploadVariantImage } from '@/hooks/product'
import { useSetStock } from '@/hooks/stock'
import type { Product, Variant } from '@/services/product/types'
import type { Stock } from '@/services/stock/types'
import { groupDigits, pendingChanges } from './pending-changes'

const SYMBOL: Record<string, string> = { VND: '₫', USD: '$' }

/**
 * One variant of a listing - one shape of the product - edited in one place: what it is, what it
 * costs in each currency, how many there are, and its own photograph.
 *
 * <p>
 * Every variant is its own block with a heading that says which one it is, because a page of
 * identical "VND / USD / On hand" rows repeated three times is a form nobody can tell apart.
 * </p>
 */
export function VariantEditor({
  product,
  variant,
  index,
  count,
  prices,
  stock,
  stockPending,
}: {
  product: Product
  variant: Variant
  index: number
  count: number
  /** This variant's price in every currency, read from each currency's own response - null means not sold in it. */
  prices: Record<string, number | null>
  stock: Stock | null
  stockPending: boolean
}) {
  const { t } = useTranslation('seller')
  const setPrice = useSetVariantPrice(product.id)
  const setStock = useSetStock(product.id)
  const upload = useUploadVariantImage(product.id)
  const removeImage = useRemoveVariantImage(product.id)

  const [typedPrices, setTypedPrices] = useState<Record<string, string>>({})
  const [typedOnHand, setTypedOnHand] = useState<string | undefined>(undefined)
  const [saving, setSaving] = useState(false)

  const changes = pendingChanges(
    { prices: typedPrices, onHand: typedOnHand },
    { prices, onHand: stock?.quantityOnHand ?? null },
  )
  const dirty = changes.prices.length > 0 || changes.onHand !== null

  // The server folds the product's picture into a variant that has none (specs/032), so "has its own"
  // is "differs from the product's".
  const ownImage = !!variant.imageUrl && variant.imageUrl !== product.imageUrl

  async function save() {
    setSaving(true)
    try {
      for (const { currency, amount } of changes.prices) {
        await setPrice.mutateAsync({ variantId: variant.id, currency, amount })
      }
      if (changes.onHand !== null) {
        await setStock.mutateAsync({ variantId: variant.id, quantityOnHand: changes.onHand })
      }
      setTypedPrices({})
      setTypedOnHand(undefined)
      toast.success(t('variant.saved'))
    } catch {
      // Shown below by ServerError, in the server's own words.
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="grid gap-4" aria-labelledby={`variant-${variant.id}`}>
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="grid gap-1.5">
          <h3 id={`variant-${variant.id}`} className="text-muted-foreground text-xs font-semibold tracking-wide uppercase">
            {count > 1 ? t('variant.heading', { index: index + 1, count }) : t('variant.only')}
          </h3>
          <div className="flex flex-wrap items-center gap-1.5">
            {variant.options.length === 0 ? (
              <span className="font-medium">{product.name}</span>
            ) : (
              variant.options.map((option) => (
                <Badge key={option.id} variant="secondary" className="h-6 rounded-full px-2.5 text-sm">
                  <span className="text-muted-foreground font-normal">{option.name}:</span> {option.value}
                </Badge>
              ))
            )}
          </div>
          <p className="text-muted-foreground font-mono text-xs">{variant.sku}</p>
        </div>
        <StockBadge available={stock?.quantityAvailable} reserved={stock?.quantityReserved} pending={stockPending} />
      </header>

      <div className="grid gap-4 sm:grid-cols-[6.5rem_1fr]">
        <div className="grid content-start gap-1.5">
          <ImageDropzone
            compact
            label={t('variant.photoOf', { name: variant.optionSummary || variant.sku })}
            busy={upload.isPending}
            onFile={(file) => upload.mutate({ variantId: variant.id, file })}
            preview={<ProductImage product={product} imageUrl={variant.imageUrl} thumb />}
          />
          {ownImage && (
            <Button
              variant="ghost"
              size="xs"
              className="text-muted-foreground rounded-full"
              disabled={removeImage.isPending}
              onClick={() => removeImage.mutate(variant.id)}
            >
              <ImageOffIcon /> {t('variantImage.remove')}
            </Button>
          )}
        </div>

        <div className="grid content-start gap-3">
          <div className="grid gap-3 sm:grid-cols-3">
            {CURRENCIES.map((currency) => {
              const current = prices[currency] ?? null
              const inputId = `price-${variant.id}-${currency}`

              return (
                <div key={currency} className="grid gap-1.5">
                  <Label htmlFor={inputId} className="text-xs">
                    {t('variant.priceIn', { currency })}
                  </Label>
                  <InputGroup className="h-10 rounded-xl">
                    <InputGroupAddon>
                      <InputGroupText>{SYMBOL[currency] ?? currency}</InputGroupText>
                    </InputGroupAddon>
                    <InputGroupInput
                      id={inputId}
                      inputMode="decimal"
                      placeholder={current === null ? t('listing.noPrice') : undefined}
                      value={typedPrices[currency] ?? (current === null ? '' : groupDigits(current))}
                      onChange={(event) => setTypedPrices((p) => ({ ...p, [currency]: event.target.value }))}
                    />
                  </InputGroup>
                </div>
              )
            })}

            <div className="grid gap-1.5">
              <Label htmlFor={`onhand-${variant.id}`} className="text-xs">
                {t('stock.onHand')}
              </Label>
              <InputGroup className="h-10 rounded-xl">
                <InputGroupInput
                  id={`onhand-${variant.id}`}
                  inputMode="numeric"
                  disabled={!stock}
                  placeholder={stock ? undefined : '—'}
                  value={typedOnHand ?? (stock ? String(stock.quantityOnHand) : '')}
                  onChange={(event) => setTypedOnHand(event.target.value)}
                />
              </InputGroup>
            </div>
          </div>

          {!stock && !stockPending && <p className="text-muted-foreground text-xs">{t('stock.notRegisteredYet')}</p>}
          <ServerError error={setPrice.error ?? setStock.error ?? upload.error ?? removeImage.error} fallback={t('listing.loadFailed')} />

          <div className="flex items-center justify-end gap-2">
            {dirty && (
              <Button
                variant="ghost"
                className="rounded-full"
                onClick={() => {
                  setTypedPrices({})
                  setTypedOnHand(undefined)
                }}
              >
                {t('action.cancel', { ns: 'common' })}
              </Button>
            )}
            <Button className="rounded-full px-4" disabled={!dirty || saving} onClick={() => void save()}>
              <SaveIcon /> {saving ? t('action.saving', { ns: 'common' }) : t('variant.save')}
            </Button>
          </div>
        </div>
      </div>
    </section>
  )
}
