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
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Product</TableHead>
          <TableHead>Price</TableHead>
          <TableHead className="w-28">Quantity</TableHead>
          <TableHead>Total</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {cart.lines.map((line) => {
          const problem = lineProblem(line.status)

          return (
            <TableRow key={line.productId}>
              <TableCell className="align-top">
                <Link to={`/products/${line.productId}`} className="hover:underline">
                  {line.name ?? 'Unknown product'}
                </Link>
                {problem && <div className="text-destructive text-xs">{problem}</div>}
              </TableCell>
              <TableCell className="align-top">{line.unitPrice === null ? '-' : money(line.unitPrice)}</TableCell>
              <TableCell className="align-top">
                <Input
                  key={line.quantity} // re-read from the server after every change
                  type="number"
                  min={1}
                  className="w-20"
                  aria-label={`Quantity of ${line.name ?? 'this product'}`}
                  defaultValue={line.quantity}
                  disabled={busy}
                  onBlur={(event) => {
                    const next = Number(event.target.value)
                    if (Number.isInteger(next) && next > 0 && next !== line.quantity) {
                      onQuantityChange(line.productId, next)
                    } else {
                      event.target.value = String(line.quantity)
                    }
                  }}
                />
              </TableCell>
              <TableCell className="align-top">{line.lineTotal === null ? '-' : money(line.lineTotal)}</TableCell>
              <TableCell className="align-top">
                <Button variant="ghost" size="sm" disabled={busy} onClick={() => onRemove(line.productId)}>
                  Remove
                </Button>
              </TableCell>
            </TableRow>
          )
        })}
      </TableBody>
    </Table>
  )
}
