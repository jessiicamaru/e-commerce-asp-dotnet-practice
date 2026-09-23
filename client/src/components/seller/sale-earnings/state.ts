import type { Sale } from '@/services/order/types'

/**
 * Where a sale's money is (specs/037): never recorded, on its way (paid, parcel not sent), due (sent,
 * not paid out) or paid out. The same rule the server's balance uses, so a sale and the balance it is
 * counted in never disagree about which column it is in.
 */
export type EarningState = 'unrecorded' | 'cancelled' | 'onTheWay' | 'due' | 'paidOut'

export function earningState(sale: Pick<Sale, 'payout' | 'paidOut' | 'status'>): EarningState {
  if (sale.payout === null) {
    return 'unrecorded'
  }

  // A cancelled order is never money (specs/039), whatever terms it recorded.
  if (sale.status === 'Cancelled') {
    return 'cancelled'
  }

  if (sale.paidOut) {
    return 'paidOut'
  }

  return sale.status === 'Shipped' ? 'due' : 'onTheWay'
}
