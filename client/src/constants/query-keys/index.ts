import type { ProductQuery } from '@/services/product/types'
import type { CheckoutChoice } from '@/services/order/types'

/**
 * Every TanStack Query key in one place, so a mutation can invalidate what it affects without
 * guessing how a hook spelled its key.
 */
export const queryKeys = {
  products: (query: ProductQuery) => ['products', query] as const,
  product: (id: string) => ['product', id] as const,
  categories: () => ['categories'] as const,
  cart: () => ['cart'] as const,
  addresses: () => ['addresses'] as const,
  shippingOptions: () => ['shipping-options'] as const,
  checkoutQuote: (choice: CheckoutChoice) => ['checkout-quote', choice] as const,
  order: (id: string) => ['order', id] as const,
  myOrders: (page: number) => ['orders', page] as const,
  health: () => ['health'] as const,
}
