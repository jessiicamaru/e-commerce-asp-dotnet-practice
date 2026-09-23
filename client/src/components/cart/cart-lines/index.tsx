import { Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Trash2Icon } from 'lucide-react'
import { ProductImage } from '@/components/product/product-image'
import { QuantityStepper } from '@/components/shared/quantity-stepper'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import type { Cart } from '@/services/cart/types'
import { lineProblem } from '@/utils/cart'
import { money } from '@/utils/shared'

/**
 * The cart's lines, each with its picture: somebody who does not follow cameras will not remember
 * which "X-T5 kit" they added, and a photograph settles it where a name cannot.
 *
 * Every change is sent at once and the cart is then re-read, so the numbers shown are always the
 * server's - the names and prices on a cart come from Catalog at read time (specs/010).
 */
export function CartLines({
  cart,
  images,
  busy,
  onQuantityChange,
  onRemove,
}: {
  cart: Cart
  /** The picture per variant id, from `useCartImages`. Missing means the tile. */
  images: Record<string, string | null>
  busy: boolean
  onQuantityChange: (variantId: string, quantity: number) => void
  onRemove: (variantId: string) => void
}) {
  const { t } = useTranslation('cart')

  return (
    <ul className="grid gap-4">
      {cart.lines.map((line, index) => {
        const problem = lineProblem(t, line.status)
        const name = line.name ?? t('unknownProduct')

        return (
          <Fragment key={line.variantId}>
            {index > 0 && <Separator />}
            <li className="grid grid-cols-[5rem_1fr] gap-4 sm:grid-cols-[6rem_1fr]">
              <Link to={`/products/${line.productId}`} tabIndex={-1} aria-hidden="true">
                <ProductImage
                  product={{ id: line.productId, name, imageUrl: images[line.variantId] ?? null }}
                  thumb
                />
              </Link>

              <div className="grid min-w-0 gap-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="grid min-w-0 gap-0.5">
                    <Link to={`/products/${line.productId}`} className="font-medium hover:underline">
                      {name}
                    </Link>
                    {line.optionSummary && <span className="text-muted-foreground text-sm">{line.optionSummary}</span>}
                    {line.unitPrice !== null && (
                      <span className="text-muted-foreground text-sm">{money(line.unitPrice, cart.currency)}</span>
                    )}
                    {problem && <span className="text-destructive text-sm">{problem}</span>}
                  </div>
                  <span className="shrink-0 font-semibold">
                    {line.lineTotal === null ? '-' : money(line.lineTotal, cart.currency)}
                  </span>
                </div>

                <div className="flex items-center justify-between gap-3">
                  <QuantityStepper
                    size="sm"
                    label={`${t('columns.quantity')}: ${name}`}
                    value={line.quantity}
                    disabled={busy}
                    onChange={(quantity) => onQuantityChange(line.variantId, quantity)}
                  />
                  <Button
                    variant="ghost"
                    size="sm"
                    className="text-muted-foreground hover:text-destructive rounded-full"
                    disabled={busy}
                    onClick={() => onRemove(line.variantId)}
                  >
                    <Trash2Icon /> {t('remove')}
                  </Button>
                </div>
              </div>
            </li>
          </Fragment>
        )
      })}
    </ul>
  )
}
