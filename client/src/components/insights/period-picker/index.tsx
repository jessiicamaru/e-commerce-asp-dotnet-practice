import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { PERIODS, type Period } from '@/services/insights/types'

/** The last 7, 30 or 90 days (specs/047, 068) - one choice at a time, the chosen one pressed. */
export function PeriodPicker({ period, onChange }: { period: Period; onChange: (period: Period) => void }) {
  const { t } = useTranslation('common')

  return (
    <div className="bg-card ring-border/60 flex gap-1 justify-self-start rounded-full p-1 ring-1">
      {PERIODS.map((p) => (
        <Button
          key={p}
          size="sm"
          variant={p === period ? 'default' : 'ghost'}
          className="rounded-full"
          aria-pressed={p === period}
          onClick={() => onChange(p)}
        >
          {t('insights.period', { count: p })}
        </Button>
      ))}
    </div>
  )
}
