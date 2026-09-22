import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table'
import { useHealth } from '@/hooks/health'
import { cn } from '@/utils/shared'

/**
 * Every service's `/health`, through the gateway's health routes. The first page this storefront had,
 * and still the quickest proof that `/api` reaches the gateway and the gateway reaches everything.
 */
export function StatusPage() {
  const { t } = useTranslation('status')
  const services = useHealth()

  return (
    <section>
      <h1 className="mb-2 text-2xl font-bold">{t('title')}</h1>
      <p className="text-muted-foreground mb-4 text-sm">{t('subtitle')}</p>

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
                {pending || !health
                  ? t('checking')
                  : health.up
                    ? t('up')
                    : t('down', { detail: health.detail })}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <p className="text-muted-foreground mt-4 text-sm">{t('orchestratorNote')}</p>
    </section>
  )
}
