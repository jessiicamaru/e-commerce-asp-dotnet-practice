/** The six services that hold something about a person (specs/111), each answering for itself. */
export const MY_DATA_SERVICES = ['identity', 'catalog', 'order', 'cart', 'payment', 'activity'] as const
export type MyDataService = (typeof MY_DATA_SERVICES)[number]

export interface WithheldTable {
  table: string
  reason: string
}

/** One service's answer: its sections of rows, and what it keeps back and why. */
export interface ServiceExport {
  service: string
  exportedAt: string
  sections: Record<string, unknown[]>
  withheld: WithheldTable[]
}

/** The file a person downloads: every service's answer, or `{ unavailable: true }` in its place. */
export interface MyDataFile {
  exportedAt: string
  person: { id: string; email: string }
  services: Record<MyDataService, ServiceExport | { unavailable: true }>
}
