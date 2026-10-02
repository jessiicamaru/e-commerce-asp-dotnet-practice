import type { Page } from '@ecommerce/core/services/product/types'

/** What a shopper can report (specs/101). */
export type ReportTarget = 'Review' | 'Question' | 'Product'

/** Why - a short list, so the moderators' queue can be sorted rather than read. */
export const REPORT_REASONS = ['Spam', 'Offensive', 'Misleading', 'Counterfeit', 'Other'] as const
export type ReportReason = (typeof REPORT_REASONS)[number]

export interface NewReport {
  targetType: ReportTarget
  targetId: string
  reason: ReportReason
  details: string | null
}

/** One reported thing in the moderators' queue: every open report of it, counted. */
export interface ReportedItem {
  targetType: ReportTarget
  targetId: string
  productId: string
  productName: string
  /** The review or question as it reads now, or the product's name. */
  excerpt: string | null
  reportCount: number
  reasons: Partial<Record<ReportReason, number>>
  /** The latest few in the reporters' own words. */
  details: string[]
  firstReportedAt: string
  lastReportedAt: string
}

export type ReportedItemPage = Page<ReportedItem>
