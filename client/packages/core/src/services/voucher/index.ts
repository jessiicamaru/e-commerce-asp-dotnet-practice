// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@ecommerce/core/config/axios'
import type { NewVoucher, PublicVoucher, PublicVoucherScope, VoucherEdit, VoucherPage, VoucherSummary, VoucherFilter } from './types'

/**
 * Vouchers (specs/069, 070): an administrator's are the platform's, a seller's their shop's. Nothing here names
 * an owner - the server reads it from the token, and refuses anybody who is neither.
 */
export class Voucher {
  static async create(voucher: NewVoucher): Promise<VoucherSummary> {
    const { data } = await http.post<VoucherSummary>('/vouchers', voucher)
    return data
  }

  /** The live public vouchers in a scope (specs/114) - anonymous, in the currency being browsed in. */
  static async public(scope: PublicVoucherScope): Promise<PublicVoucher[]> {
    const params = new URLSearchParams()
    if (scope.platform) params.set('platform', 'true')
    for (const id of scope.sellerIds ?? []) params.append('sellerId', id)
    if (scope.productId) params.set('productId', scope.productId)
    for (const id of scope.variantIds ?? []) params.append('variantId', id)
    const { data } = await http.get<PublicVoucher[]>(`/vouchers/public?${params}`, { anonymous: true })
    return data
  }

  /** The caller's own: the platform's for an administrator, theirs for a seller. Newest first. */
  static async mine(page: number, pageSize: number, filter: VoucherFilter = {}): Promise<VoucherPage> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    // By code or name and by state (specs/133); left out when empty.
    if (filter.search) params.set('search', filter.search)
    if (filter.state) params.set('state', filter.state)
    const { data } = await http.get<VoucherPage>(`/vouchers/mine?${params}`)
    return data
  }

  /** Corrects an active voucher's name, end, limits and minimums (specs/113). */
  static async edit(id: string, edit: VoucherEdit): Promise<VoucherSummary> {
    const { data } = await http.put<VoucherSummary>(`/vouchers/${id}`, edit)
    return data
  }

  /** From now on, not usable. Orders that used it keep it. */
  static async disable(id: string): Promise<VoucherSummary> {
    const { data } = await http.post<VoucherSummary>(`/vouchers/${id}/disable`)
    return data
  }
}
