import { VoucherPage } from '@/components/voucher/voucher-page'

/** A seller's own shop vouchers (specs/070): on their lines only, paid for out of their payout. */
export function ShopVouchersPage() {
  return <VoucherPage platform={false} />
}
