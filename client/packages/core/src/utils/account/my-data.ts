import { MY_DATA_SERVICES, type MyDataFile, type MyDataService, type ServiceExport } from '@ecommerce/core/services/my-data/types'

/**
 * One file from the six answers (specs/111). A service that did not answer is marked, never left out: a file that
 * silently lacks the orders reads as "the shop holds no orders about me", which is untrue.
 */
export function composeMyData(
  person: { id: string; email: string },
  answers: Record<MyDataService, PromiseSettledResult<ServiceExport>>,
  now: Date,
): { file: MyDataFile; unavailable: MyDataService[] } {
  const services = {} as MyDataFile['services']
  const unavailable: MyDataService[] = []
  for (const service of MY_DATA_SERVICES) {
    const answer = answers[service]
    if (answer.status === 'fulfilled') services[service] = answer.value
    else {
      services[service] = { unavailable: true }
      unavailable.push(service)
    }
  }
  return { file: { exportedAt: now.toISOString(), person: { id: person.id, email: person.email }, services }, unavailable }
}

/** `my-data-2026-10-01.json` - the day in the reader's own calendar, which is the one they will look for. */
export function myDataFileName(now: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `my-data-${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}.json`
}
