import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ErrorMessage, LoadingRows } from '@/components/shared/query-state'

export interface RankedRow {
  key: string
  label: ReactNode
  value: string
}

/**
 * A numbered "top N" list (specs/047, 068): what sold, what was looked at, who bought. `rows` undefined is
 * still loading; an empty list says so rather than drawing nothing.
 */
export function RankedList({ rows, failed }: { rows?: RankedRow[]; failed: boolean }) {
  const { t } = useTranslation('common')
  if (failed) return <ErrorMessage>{t('insights.loadFailed')}</ErrorMessage>
  if (!rows) return <LoadingRows rows={3} />
  if (rows.length === 0) return <p className="text-muted-foreground text-sm">{t('insights.none')}</p>

  return (
    <ol className="grid gap-2 text-sm">
      {rows.map((row, index) => (
        <li key={row.key} className="grid grid-cols-[1.5rem_1fr_auto] items-baseline gap-2">
          <span className="text-muted-foreground tabular-nums">{index + 1}</span>
          <span className="min-w-0 truncate">{row.label}</span>
          <span className="text-muted-foreground text-xs tabular-nums">{row.value}</span>
        </li>
      ))}
    </ol>
  )
}
