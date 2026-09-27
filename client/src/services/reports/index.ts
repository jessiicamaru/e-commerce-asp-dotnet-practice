// The model types live in ./types, imported from there: this file's export is the class.
import { http } from '@/config/axios'
import type { NewReport, ReportedItemPage, ReportTarget } from './types'

/**
 * Reporting what should not be on the shop, and the queue staff work through (specs/101). Who reports comes from the
 * token. Acting on a report is the existing hide or take-down, which closes the reports itself; dismissing is here.
 */
export class Reports {
  static async create(report: NewReport): Promise<void> {
    await http.post('/reports', report)
  }

  static async queue(page: number, pageSize: number): Promise<ReportedItemPage> {
    const { data } = await http.get<ReportedItemPage>(`/reports?pageNumber=${page}&pageSize=${pageSize}`)
    return data
  }

  static async dismiss(targetType: ReportTarget, targetId: string): Promise<void> {
    await http.post(`/reports/${targetType}/${targetId}/dismiss`)
  }
}
