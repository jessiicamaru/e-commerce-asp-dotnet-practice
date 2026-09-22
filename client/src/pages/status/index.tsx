import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table'
import { useHealth } from '@/hooks/health'
import { cn } from '@/utils/shared'

/**
 * Every service's `/health`, through the gateway's health routes. The first page this storefront had,
 * and still the quickest proof that `/api` reaches the gateway and the gateway reaches everything.
 */
export function StatusPage() {
  const services = useHealth()

  return (
    <section>
      <h1 className="mb-2 text-2xl font-bold">Backend status</h1>
      <p className="text-muted-foreground mb-4 text-sm">Each service's health, checked through the gateway.</p>

      <Table className="max-w-sm">
        <TableBody>
          {services.map(({ service, pending, health }) => (
            <TableRow key={service}>
              <TableCell>{service}</TableCell>
              <TableCell
                className={cn(
                  'text-right',
                  health?.up === true && 'text-emerald-700 dark:text-emerald-400',
                  health?.up === false && 'text-destructive',
                )}
              >
                {pending || !health ? 'checking…' : health.up ? 'up' : `down (${health.detail})`}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <p className="text-muted-foreground mt-4 text-sm">
        The orchestrator has no health endpoint - it has no HTTP surface at all - so it cannot be listed here.
      </p>
    </section>
  )
}
