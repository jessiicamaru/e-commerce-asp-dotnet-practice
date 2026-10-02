import { http } from '@ecommerce/core/config/axios'

/**
 * What Payment says about itself (specs/134, #253): its own `/health` names the provider, and "Stub" is one of the three
 * signals this shop keeps so a stand-in is never taken for the real thing. Checkout reads it rather than saying "no money
 * is moved" on its own - a sentence written here would outlive the stub, or be deleted while the stub was still there.
 */
export class Payment {
  /** Whether payments go through the stand-in; null when Payment cannot be asked. */
  static async isStub(): Promise<boolean | null> {
    try {
      const { data } = await http.get<{ provider?: string }>('/payment/health', { anonymous: true })
      return typeof data.provider === 'string' ? data.provider.startsWith('Stub') : null
    } catch {
      return null
    }
  }
}
