import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table'
import type { OrderLine } from '@/services/order/types'
import { money } from '@/utils/shared'

/** The lines of an order or of a quote: the price is frozen on the order line (specs/009). */
export function OrderLines({ items }: { items: OrderLine[] }) {
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
              {item.quantity} × {money(item.unitPrice)}
            </TableCell>
            <TableCell className="text-right">{money(item.totalPrice)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
