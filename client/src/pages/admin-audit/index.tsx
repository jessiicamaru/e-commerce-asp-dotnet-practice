import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { SearchIcon } from 'lucide-react'
import { PageTitle } from '@/components/seller/page-title'
import { Pager } from '@/components/shared/pager'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui/input-group'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAuditLog, useAuditSummary } from '@/hooks/audit'
import { AUDIT_CATEGORIES, type AuditCategory, type AuditFilter } from '@/services/audit/types'
import { PAGE_SIZE } from '@/constants/shared'
import { cn } from '@/utils/shared'
import { AuditEntryDialog } from './entry-dialog'
import { PERIODS, humanise, periodStart, type Period } from './format'

/**
 * Who did what (specs/041): the audit log, newest first - a tab per category with how many each holds in
 * the period, filters by actor and action, and each entry opening to what changed, field by field.
 *
 * <p>
 * Everything the reader chose is in the address, so a filtered view can be shared with the next
 * administrator - "look at what this account did last week" is a link.
 * </p>
 */
export function AdminAuditPage() {
  const { t, i18n } = useTranslation('admin')
  const [params, setParams] = useSearchParams()
  const asked = params.get('category')
  const category = AUDIT_CATEGORIES.includes(asked as AuditCategory) ? (asked as AuditCategory) : undefined
  const period = (PERIODS.includes(params.get('period') as Period) ? params.get('period') : 'week') as Period
  const actor = params.get('actor') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const [actorDraft, setActorDraft] = useState(actor)
  const [open, setOpen] = useState<string | null>(null)

  // The period is turned into a time once per choice, not per render - a "from" that moved every render
  // would be a new query key, and a new request, every time.
  const from = useMemo(() => periodStart(period), [period])
  const filter: AuditFilter = { category, actor: actor || undefined, from }

  const log = useAuditLog(filter, page, PAGE_SIZE)
  const summary = useAuditSummary(from)
  const counts = new Map(summary.data?.map((c) => [c.category, c.count]))
  const total = summary.data?.reduce((sum, c) => sum + c.count, 0)

  const update = (next: Record<string, string | undefined>) => {
    const merged = new URLSearchParams(params)
    for (const [key, value] of Object.entries(next)) {
      if (value) merged.set(key, value)
      else merged.delete(key)
    }
    merged.delete('page')
    if (next.page) merged.set('page', next.page)
    setParams(merged)
  }

  return (
    <section className="grid gap-6">
      <PageTitle title={t('audit.title')} subtitle={t('audit.subtitle')} />

      <div role="tablist" className="bg-card ring-border/60 flex flex-wrap gap-1 justify-self-start rounded-3xl p-1 ring-1">
        {[undefined, ...AUDIT_CATEGORIES].map((c) => (
          <button
            key={c ?? 'all'}
            type="button"
            role="tab"
            aria-selected={c === category}
            onClick={() => update({ category: c })}
            className={cn(
              'flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium transition-colors',
              c === category ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`audit.category.${c ?? 'All'}`)}
            <span className="text-xs tabular-nums opacity-70">{c ? (counts.get(c) ?? 0) : (total ?? '')}</span>
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <form
          className="min-w-64 flex-1"
          onSubmit={(event) => {
            event.preventDefault()
            update({ actor: actorDraft.trim() || undefined })
          }}
        >
          <InputGroup className="h-10 rounded-full">
            <InputGroupAddon>
              <SearchIcon />
            </InputGroupAddon>
            <InputGroupInput
              aria-label={t('audit.actorFilter')}
              placeholder={t('audit.actorFilter')}
              value={actorDraft}
              onChange={(event) => setActorDraft(event.target.value)}
            />
          </InputGroup>
        </form>
        <div className="bg-card ring-border/60 flex gap-1 rounded-full p-1 ring-1">
          {PERIODS.map((p) => (
            <Button
              key={p}
              size="sm"
              variant={p === period ? 'default' : 'ghost'}
              className="rounded-full"
              aria-pressed={p === period}
              onClick={() => update({ period: p })}
            >
              {t(`audit.period.${p}`)}
            </Button>
          ))}
        </div>
      </div>

      {log.isError ? (
        <ErrorMessage>{t('audit.loadFailed')}</ErrorMessage>
      ) : log.isPending || !log.data ? (
        <LoadingRows />
      ) : log.data.totalCount === 0 ? (
        <p className="bg-card ring-border/60 rounded-3xl p-8 text-sm ring-1">{t('audit.none')}</p>
      ) : (
        <>
          <div className="bg-card ring-border/60 overflow-x-auto rounded-3xl p-2 ring-1">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('audit.columns.when')}</TableHead>
                  <TableHead>{t('audit.columns.what')}</TableHead>
                  <TableHead>{t('audit.columns.who')}</TableHead>
                  <TableHead className="text-right">{t('audit.columns.changes')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {log.data.items.map((entry) => (
                  <TableRow key={entry.id} className="cursor-pointer" onClick={() => setOpen(entry.id)}>
                    <TableCell className="text-muted-foreground text-xs whitespace-nowrap">
                      {new Date(entry.occurredAt).toLocaleString(i18n.language)}
                    </TableCell>
                    <TableCell className="whitespace-normal">
                      <div className="flex flex-wrap items-center gap-2">
                        <Badge variant="outline">{t(`audit.category.${entry.category}`)}</Badge>
                        <button type="button" className="font-medium hover:underline" onClick={() => setOpen(entry.id)}>
                          {t(`audit.action.${entry.action}`, { defaultValue: humanise(entry.action) })}
                        </button>
                      </div>
                      <p className="text-muted-foreground text-xs">{entry.summary}</p>
                    </TableCell>
                    <TableCell className="text-sm whitespace-normal">
                      {entry.actorEmail ?? t('audit.system')}
                      {entry.actorRole && <span className="text-muted-foreground text-xs"> · {entry.actorRole}</span>}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{entry.changeCount || '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
          <Pager
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={log.data.totalCount}
            onChange={(next) => update({ page: String(next) })}
          />
        </>
      )}

      <AuditEntryDialog id={open} onClose={() => setOpen(null)} />
    </section>
  )
}
