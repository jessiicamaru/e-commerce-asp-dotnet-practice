import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { Cart } from '@/services/cart/types'
import { lineProblem } from '@/utils/cart'
import { money } from '@/utils/shared'

/**
 * The cart's lines. Quantity is sent on blur and the cart is then re-read, so the numbers shown are
 * always the server's.
 */
export function CartLines({
  cart,
  busy,
  onQuantityChange,
  onRemove,
}: {
  cart: Cart
  busy: boolean
  onQuantityChange: (productId: string, quantity: number) => void
  onRemove: (productId: string) => void
}) {
  const { t } = useTranslation('cart')

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('columns.product')}</TableHead>
          <TableHead>{t('columns.price')}</TableHead>
          <TableHead className="w-28">{t('columns.quantity')}</TableHead>
          <TableHead>{t('columns.total')}</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {cart.lines.map((line) => {
          const problem = lineProblem(t, line.status)

          return (
            <TableRow key={line.variantId}>
              <TableCell className="align-top">
                <Link to={`/products/${line.productId}`} className="hover:underline">
                  {line.name ?? t('unknownProduct')}
                </Link>
                {line.optionSummary && (
                  <div className="text-muted-foreground text-xs">{line.optionSummary}</div>
                )}
                {problem && <div className="text-destructive text-xs">{problem}</div>}
              </TableCell>
              <TableCell className="align-top">
                {line.unitPrice === null ? '-' : money(line.unitPrice, cart.currency)}
              </TableCell>
              <TableCell className="align-top">
                <Input
                  key={line.quantity} // re-read from the server after every change
                  type="number"
                  min={1}
                  className="w-20"
                  aria-label={`${t('columns.quantity')}: ${line.name ?? ''}`}
                  defaultValue={line.quantity}
                  disabled={busy}
                  onBlur={(event) => {
                    const next = Number(event.target.value)
                    if (Number.isInteger(next) && next > 0 && next !== line.quantity) {
                      onQuantityChange(line.variantId, next)
                    } else {
                      event.target.value = String(line.quantity)
                    }
                  }}
                />
              </TableCell>
              <TableCell className="align-top">
                {line.lineTotal === null ? '-' : money(line.lineTotal, cart.currency)}
              </TableCell>
              <TableCell className="align-top">
                <Button variant="ghost" size="sm" disabled={busy} onClick={() => onRemove(line.variantId)}>
                  {t('remove')}
                </Button>
              </TableCell>
            </TableRow>
          )
        })}
      </TableBody>
    </Table>
  )
}
