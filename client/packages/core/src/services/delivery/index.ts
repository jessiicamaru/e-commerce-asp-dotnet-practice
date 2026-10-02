// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@ecommerce/core/config/axios'
import type { Carrier, DeliveryOption, DeliverySettings } from './types'

/**
 * Delivery options and the carrier (specs/098). The carrier is public - every page that shows a tracking reference links
 * it; the settings are an administrator's.
 */
export class Delivery {
  static async carrier(): Promise<Carrier> {
    const { data } = await http.get<Carrier>('/orders/delivery/carrier', { anonymous: true })
    return data
  }

  static async settings(): Promise<DeliverySettings> {
    const { data } = await http.get<DeliverySettings>('/orders/delivery')
    return data
  }

  /** Creates the option under a new code or changes it; the code never changes. */
  static async saveOption(option: DeliveryOption): Promise<DeliveryOption> {
    const { code, ...body } = option
    const { data } = await http.put<DeliveryOption>(`/orders/delivery/options/${code}`, body)
    return data
  }

  static async saveCarrier(carrier: Carrier): Promise<Carrier> {
    const { data } = await http.put<Carrier>('/orders/delivery/carrier', carrier)
    return data
  }
}
