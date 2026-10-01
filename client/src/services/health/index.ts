import { http } from '@/config/axios'

/**
 * Each service's own `/health`, through the gateway's health routes - every one of them, held to the gateway's
 * `*-health-route` entries by a test (specs/121, #244). The orchestrator answers since specs/071; Activity since
 * specs/041. Both were missing, behind a note saying the orchestrator could not be asked.
 */
export const HEALTH_SERVICES = ['identity', 'catalog', 'order', 'orchestrator', 'inventory', 'payment', 'cart', 'activity'] as const

export type HealthService = (typeof HEALTH_SERVICES)[number]

export type ServiceHealth = { up: true } | { up: false; detail: string }

export class Health {
  static async check(service: HealthService): Promise<ServiceHealth> {
    try {
      const response = await http.get(`/${service}/health`, { anonymous: true })
      return response.status < 400 ? { up: true } : { up: false, detail: String(response.status) }
    } catch (error) {
      const status = (error as { status?: number }).status
      return { up: false, detail: status ? String(status) : 'unreachable' }
    }
  }
}
