/**
 * The order a gateway's reference names (specs/143): VNPay's `vnp_TxnRef` is the order id without its dashes. Anything
 * else is not one of this shop's references, and names no order.
 */
export function orderIdFromReference(reference: string | null | undefined): string | null {
  if (!reference || !/^[0-9a-f]{32}$/i.test(reference)) return null
  const r = reference.toLowerCase()
  return `${r.slice(0, 8)}-${r.slice(8, 12)}-${r.slice(12, 16)}-${r.slice(16, 20)}-${r.slice(20)}`
}
