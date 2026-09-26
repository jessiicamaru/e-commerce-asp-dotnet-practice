import type { ProductQuery } from '@/services/product/types'
import type { CheckoutChoice } from '@/services/order/types'
import type { QueueState } from '@/services/admin/types'
import type { AuditFilter } from '@/services/audit/types'

/**
 * Every TanStack Query key in one place, so a mutation can invalidate what it affects without
 * guessing how a hook spelled its key.
 */
export const queryKeys = {
  products: (query: ProductQuery) => ['products', query] as const,
  product: (id: string) => ['product', id] as const,
  myProducts: (query: ProductQuery) => ['my-products', query] as const,
  categories: () => ['categories'] as const,
  myShop: () => ['my-shop'] as const,
  cart: () => ['cart'] as const,
  addresses: () => ['addresses'] as const,
  me: () => ['me'] as const,
  shippingOptions: () => ['shipping-options'] as const,
  checkoutQuote: (choice: CheckoutChoice) => ['checkout-quote', choice] as const,
  order: (id: string) => ['order', id] as const,
  myOrders: (page: number) => ['orders', page] as const,
  mySales: (page: number) => ['sales', page] as const,
  sale: (id: string) => ['sale', id] as const,
  balance: () => ['balance'] as const,
  payouts: (page: number) => ['payouts', page] as const,
  adminQueue: (status: QueueState, page: number) => ['admin-queue', status, page] as const,
  adminOrder: (id: string) => ['admin-order', id] as const,
  payoutsDue: () => ['payouts-due'] as const,
  adminReturns: (status: string, page: number) => ['admin-returns', status, page] as const,
  myVouchers: (page: number) => ['vouchers', 'mine', page] as const,
  savedIds: () => ['saved', 'ids'] as const,
  savedProducts: (page: number) => ['saved', 'list', page] as const,
  productQuestions: (productId: string, page: number) => ['questions', 'product', productId, page] as const,
  questionQueue: (answered: boolean, page: number) => ['questions', 'queue', answered, page] as const,
  staffQuestions: (hidden: boolean, page: number) => ['questions', 'staff', hidden, page] as const,
  emailTemplates: () => ['email-templates'] as const,
  outgoingEmails: (status: string, search: string, page: number) => ['outgoing-emails', status, search, page] as const,
  notificationWording: () => ['notification-wording', 'current'] as const,
  wordingOverview: () => ['notification-wording', 'all'] as const,
  wordingVersions: (key: string, language: string) => ['notification-wording', key, language, 'versions'] as const,
  emailTemplateVersions: (template: string, language: string) => ['email-templates', template, language, 'versions'] as const,
  auditLog: (filter: AuditFilter, page: number) => ['audit-log', filter, page] as const,
  accounts: (search: string, page: number) => ['accounts', search, page] as const,
  myShopApplications: ['shop-applications', 'mine'] as const,
  reviewQueue: (status: string, page: number) => ['review-queue', status, page] as const,
  myDecisions: ['my-decisions'] as const,
  insights: (part: string, from: string) => ['insights', part, from] as const,
  people: (ids: string[]) => ['people', ...ids] as const,
  productReviews: (productId: string, page: number) => ['reviews', productId, page] as const,
  myReview: (productId: string) => ['reviews', productId, 'mine'] as const,
  staffReviews: (hidden: boolean, page: number) => ['staff-reviews', hidden, page] as const,
  shopApplications: (status: string, page: number) => ['shop-applications', status, page] as const,
  auditEntry: (id: string) => ['audit-entry', id] as const,
  auditSummary: (from: string) => ['audit-summary', from] as const,
  unreadCount: () => ['notifications', 'unread-count'] as const,
  notifications: (page: number, unreadOnly: boolean) => ['notifications', 'list', page, unreadOnly] as const,
  health: () => ['health'] as const,
}
