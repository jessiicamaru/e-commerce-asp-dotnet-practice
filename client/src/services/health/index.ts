import { http } from '@/config/axios'

/**
 * Each service's own `/health`, through the gateway's health routes.
 *
 * The orchestrator is missing on purpose: it has no controllers, so it has no health endpoint at all.
 */
export const HEALTH_SERVICES = ['identity', 'catalog', 'order', 'inventory', 'payment', 'cart'] as const

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
