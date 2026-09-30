// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { MyDataService, ServiceExport } from './types'

/** Where each service answers "what do you hold about me" (specs/111), all through the gateway. */
const ADDRESSES: Record<MyDataService, string> = {
  identity: '/auth/me/data',
  catalog: '/products/my-data',
  order: '/orders/my-data',
  cart: '/cart/my-data',
  payment: '/payments/my-data',
  activity: '/notifications/my-data',
}

/** The signed-in person's own data, one service at a time - the token says whose. */
export class MyData {
  static async of(service: MyDataService): Promise<ServiceExport> {
    const { data } = await http.get<ServiceExport>(ADDRESSES[service])
    return data
  }
}
