import { useTranslation } from 'react-i18next'
import { LoadingRows } from '@/components/shared/query-state'
import { ServerError } from '@/components/shared/server-error'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAuditEntry } from '@/hooks/audit'
import { formatValue, humanise } from './format'

/**
 * One audit entry in full (specs/041): who, when, on what, from which service - and what changed, field by
 * field. The diff is the server's, computed once when the entry was recorded; nothing here recomputes it.
 */
export function AuditEntryDialog({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t, i18n } = useTranslation('admin')
  const entry = useAuditEntry(id)

  return (
    <Dialog open={id !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
        {entry.isError ? (
          <ServerError error={entry.error} fallback={t('audit.loadFailed')} />
        ) : entry.isPending || !entry.data ? (
          <LoadingRows rows={2} />
        ) : (
          <>
            <DialogHeader>
              <DialogTitle className="flex flex-wrap items-center gap-2">
                <Badge variant="outline">{t(`audit.category.${entry.data.category}`)}</Badge>
                {t(`audit.action.${entry.data.action}`, { defaultValue: humanise(entry.data.action) })}
              </DialogTitle>
              <DialogDescription>{entry.data.summary}</DialogDescription>
            </DialogHeader>

            <dl className="grid grid-cols-[8rem_1fr] gap-x-3 gap-y-1.5 text-sm">
              <dt className="text-muted-foreground">{t('audit.columns.when')}</dt>
              <dd>{new Date(entry.data.occurredAt).toLocaleString(i18n.language)}</dd>
              <dt className="text-muted-foreground">{t('audit.columns.who')}</dt>
              <dd className="break-all">
                {entry.data.actorEmail ?? t('audit.system')}
                {entry.data.actorRole && ` · ${entry.data.actorRole}`}
              </dd>
              <dt className="text-muted-foreground">{t('audit.subject')}</dt>
              <dd className="font-mono text-xs break-all">
                {entry.data.subjectType}
                {entry.data.subjectId && ` ${entry.data.subjectId}`}
              </dd>
              <dt className="text-muted-foreground">{t('audit.service')}</dt>
              <dd>{entry.data.service}</dd>
            </dl>

            {entry.data.changes.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('audit.noChanges')}</p>
            ) : (
              <div className="overflow-x-auto">
              <Table aria-label={t('audit.changes')}>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('audit.field')}</TableHead>
                    <TableHead>{t('audit.before')}</TableHead>
                    <TableHead>{t('audit.after')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {entry.data.changes.map((change) => (
                    <TableRow key={change.path}>
                      <TableCell className="font-mono text-xs">{change.path}</TableCell>
                      <TableCell className="text-destructive text-sm whitespace-normal break-all line-through decoration-1">
                        {formatValue(change.before)}
                      </TableCell>
                      <TableCell className="text-sm whitespace-normal break-all text-emerald-700 dark:text-emerald-400">
                        {formatValue(change.after)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              </div>
            )}
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
