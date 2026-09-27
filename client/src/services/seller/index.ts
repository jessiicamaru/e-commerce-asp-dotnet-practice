import { http } from '@/config/axios'
import { ApiError } from '@/config/axios'
import type { PayoutAccount, PayoutAccountInput, Shop } from './types'

/**
 * The signed-in seller's shop (specs/027).
 *
 * Every call is about the caller and sends no seller id: Identity reads who this is from the token.
 * An endpoint that took an id would let one seller rename another's shop.
 */
export class Seller {
  static async me(): Promise<Shop> {
    const { data } = await http.get<Shop>('/sellers/me')
    return data
  }

  /** Where the caller's payouts go, masked (specs/106); null until they give one. */
  static async payoutAccount(): Promise<PayoutAccount | null> {
    try {
      const { data } = await http.get<PayoutAccount>('/sellers/me/payout-account')
      return data
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  }

  static async setPayoutAccount(input: PayoutAccountInput): Promise<PayoutAccount> {
    const { data } = await http.put<PayoutAccount>('/sellers/me/payout-account', input)
    return data
  }

  static async rename(shopName: string): Promise<Shop> {
    const { data } = await http.put<Shop>('/sellers/me/shop-name', { shopName })
    return data
  }

  /** The shop's description (specs/099); null clears it. */
  static async describe(description: string | null): Promise<Shop> {
    const { data } = await http.put<Shop>('/sellers/me/description', { description })
    return data
  }
}
