import { http } from '@ecommerce/core/config/axios'
import type { PaymentAbout, PaymentCheckout } from './types'

/**
 * Payment, as the storefront meets it.
 *
 * `about` reads what Payment says about itself (specs/134, #253): its own `/health` says whether money moves - one of
 * the signals this shop keeps so a stand-in is never taken for the real thing - and whether the customer pays at a
 * gateway (VNPay, specs/143). Checkout reads it rather than saying "no money is moved" on its own: a sentence written
 * here would outlive the stub, or be deleted while the stub was still there.
 */
export class Payment {
  static async about(): Promise<PaymentAbout> {
    try {
      const { data } = await http.get<{ provider?: string; movesMoney?: boolean; configuredOutcome?: string }>(
        '/payment/health',
        { anonymous: true },
      )
      // `movesMoney` since specs/143; before it, only the stub moved none, and it said so by name.
      const movesMoney =
        typeof data.movesMoney === 'boolean'
          ? data.movesMoney
          : typeof data.provider === 'string'
            ? !data.provider.startsWith('Stub')
            : null
      return { movesMoney, atGateway: data.configuredOutcome === 'Customer' }
    } catch {
      return { movesMoney: null, atGateway: false }
    }
  }

  /** The order's payment, for its owner: waiting at the gateway with a link, or decided (specs/143). */
  static async checkout(orderId: string): Promise<PaymentCheckout> {
    const { data } = await http.get<PaymentCheckout>(`/payments/orders/${orderId}/checkout`)
    return data
  }
}
