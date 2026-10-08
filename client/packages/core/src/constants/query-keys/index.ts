import type { ProductQuery } from '@ecommerce/core/services/product/types'
import type { CheckoutChoice } from '@ecommerce/core/services/order/types'
import type { QueueState, StaffOrderQuery } from '@ecommerce/core/services/admin/types'
import type { AuditFilter } from '@ecommerce/core/services/audit/types'
import type { PublicVoucherScope } from '@ecommerce/core/services/voucher/types'

/**
 * Every TanStack Query key in one place, so a mutation can invalidate what it affects without
 * guessing how a hook spelled its key.
 */
export const queryKeys = {
  products: (query: ProductQuery) => ['products', query] as const,
  product: (id: string) => ['product', id] as const,
  myProducts: (query: ProductQuery) => ['my-products', query] as const,
  categories: () => ['categories'] as const,
  categoriesIn: (language: string) => ['categories', 'in', language] as const,
  // Under 'categories', so a change to a category refreshes them too (specs/159).
  categorySpecifications: (categoryId: string) => ['categories', 'specifications', categoryId] as const,
  myShop: () => ['my-shop'] as const,
  shopFront: (sellerId: string) => ['shop-front', sellerId] as const,
  shopState: () => ['shop-state', 'mine'] as const,
  closedShops: (page: number) => ['shops', 'closed', page] as const,
  cart: () => ['cart'] as const,
  addresses: () => ['addresses'] as const,
  me: () => ['me'] as const,
  myTwoFactor: () => ['me', 'two-factor'] as const,
  shippingOptions: () => ['shipping-options'] as const,
  carrier: () => ['carrier'] as const,
  deliverySettings: () => ['delivery-settings'] as const,
  paymentProvider: () => ['payment-provider'] as const,
  paymentCheckout: (orderId: string) => ['payment-checkout', orderId] as const,
  checkoutQuote: (choice: CheckoutChoice) => ['checkout-quote', choice] as const,
  order: (id: string) => ['order', id] as const,
  myOrders: (page: number) => ['orders', page] as const,
  mySales: (page: number) => ['sales', page] as const,
  saleReturns: (status: string, page: number) => ['sales', 'returns', status, page] as const,
  sale: (id: string) => ['sale', id] as const,
  balance: () => ['balance'] as const,
  payouts: (page: number) => ['payouts', page] as const,
  adminQueue: (status: QueueState, page: number) => ['admin-queue', status, page] as const,
  staffOrders: (query: StaffOrderQuery) => ['staff-orders', query] as const,
  adminOrder: (id: string) => ['admin-order', id] as const,
  payoutsDue: () => ['payouts-due'] as const,
  myPayoutAccount: () => ['payout-account', 'mine'] as const,
  payoutAccounts: (sellerIds: string[]) => ['payout-accounts', sellerIds] as const,
  adminReturns: (status: string, page: number) => ['admin-returns', status, page] as const,
  myVouchers: (page: number, search = '', state = '') => ['vouchers', 'mine', page, search, state] as const,
  /** A voucher state tab's count (specs/133): a page of one under its own key. */
  myVoucherCount: (state: string, search: string) => ['vouchers', 'mine-count', state, search] as const,
  publicVouchers: (scope: PublicVoucherScope | null) => ['vouchers', 'public', scope] as const,
  savedIds: () => ['saved', 'ids'] as const,
  savedProducts: (page: number) => ['saved', 'list', page] as const,
  productQuestions: (productId: string, page: number) => ['questions', 'product', productId, page] as const,
  questionQueue: (answered: boolean, page: number) => ['questions', 'queue', answered, page] as const,
  staffQuestions: (hidden: boolean, page: number) => ['questions', 'staff', hidden, page] as const,
  emailTemplates: () => ['email-templates'] as const,
  outgoingEmails: (status: string, search: string, page: number, pageSize: number) =>
    ['outgoing-emails', status, search, page, pageSize] as const,
  notificationWording: () => ['notification-wording', 'current'] as const,
  wordingOverview: () => ['notification-wording', 'all'] as const,
  wordingVersions: (key: string, language: string) => ['notification-wording', key, language, 'versions'] as const,
  emailTemplateVersions: (template: string, language: string) => ['email-templates', template, language, 'versions'] as const,
  auditLog: (filter: AuditFilter, page: number) => ['audit-log', filter, page] as const,
  accounts: (search: string, page: number, includeDeleted = false, role = '', state = '') =>
    ['accounts', search, page, includeDeleted, role, state] as const,
  /** A staff queue's count for the console's sidebar (specs/129) - its own key, never the list's. */
  staffWaiting: (queue: string) => ['staff-waiting', queue] as const,
  /** What waits for a seller (specs/131) - its own keys too. */
  sellerWaiting: (what: string) => ['seller-waiting', what] as const,
  personHistory: (id: string, page: number, pageSize: number) => ['person-history', id, page, pageSize] as const,
  myShopApplications: ['shop-applications', 'mine'] as const,
  // The page size is part of the key (specs/130): a page of one, read for a count, once stood in for the list's page.
  reviewQueue: (status: string, page: number, pageSize: number) => ['review-queue', status, page, pageSize] as const,
  myDecisions: ['my-decisions'] as const,
  insights: (part: string, from: string) => ['insights', part, from] as const,
  people: (ids: string[]) => ['people', ...ids] as const,
  productReviews: (productId: string, page: number) => ['reviews', productId, page] as const,
  myReview: (productId: string) => ['reviews', productId, 'mine'] as const,
  staffReviews: (hidden: boolean, page: number) => ['staff-reviews', hidden, page] as const,
  reportQueue: (page: number) => ['reports', page] as const,
  shopApplications: (status: string, page: number, pageSize: number) => ['shop-applications', status, page, pageSize] as const,
  auditEntry: (id: string) => ['audit-entry', id] as const,
  auditSummary: (from: string) => ['audit-summary', from] as const,
  unreadCount: () => ['notifications', 'unread-count'] as const,
  notifications: (page: number, unreadOnly: boolean) => ['notifications', 'list', page, unreadOnly] as const,
  health: () => ['health'] as const,
}
