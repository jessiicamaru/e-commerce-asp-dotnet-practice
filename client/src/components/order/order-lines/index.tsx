import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table'
import { Price } from '@/components/shared/price'
import type { OrderLine } from '@/services/order/types'

/**
 * The lines of an order or of a quote: the price is frozen on the order line (specs/009), and so is
 * the currency it is in (specs/022) - which is why the currency is passed down rather than read from
 * whatever the shopper is browsing in.
 */
export function OrderLines({ items, currency }: { items: OrderLine[]; currency?: string }) {
  return (
    <Table>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.variantId ?? item.productId}>
            <TableCell>
              {item.productName}
              {item.optionSummary && (
                <div className="text-muted-foreground text-xs">{item.optionSummary}</div>
              )}
              {item.sku && <div className="text-muted-foreground text-xs">SKU {item.sku}</div>}
            </TableCell>
            <TableCell className="text-muted-foreground">
              {item.quantity} × <Price value={item.unitPrice} currency={currency} />
            </TableCell>
            <TableCell className="text-right">
              <Price value={item.totalPrice} currency={currency} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
