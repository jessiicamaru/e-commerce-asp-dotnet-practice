export type ShopApplicationStatus = 'Pending' | 'Approved' | 'Rejected'

/** An application to sell (specs/044). The applicant fields are filled for staff only. */
export interface ShopApplication {
  id: string
  userId: string
  applicantEmail: string | null
  applicantName: string | null
  shopName: string
  description: string | null
  phone: string | null
  status: ShopApplicationStatus
  decisionReason: string | null
  createdAt: string
  decidedAt: string | null
}

export interface ShopApplicationPage {
  items: ShopApplication[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ShopApplicationInput {
  shopName: string
  description: string
  phone: string
}
